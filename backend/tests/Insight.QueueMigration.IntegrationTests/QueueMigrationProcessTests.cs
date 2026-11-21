using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;
using Xunit;

namespace Insight.QueueMigration.IntegrationTests;

public sealed class QueueMigrationProcessTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.0-alpine")
        .Build();

    private string BrokerUri => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync() => await _rabbitMq.StartAsync();

    public async Task DisposeAsync() => await _rabbitMq.DisposeAsync();

    [Fact]
    public async Task Preflight_DoesNotConsumeOrMutateMessages()
    {
        var source = QueueName();
        var destination = QueueName();
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(source, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false);
        await PublishAsync(channel, source, "preflight-message");

        var result = await RunUtilityAsync(source, destination);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No messages were read or changed", result.Output);
        var sourceState = await channel.QueueDeclarePassiveAsync(source);
        var destinationState = await channel.QueueDeclarePassiveAsync(destination);
        Assert.Equal(1u, sourceState.MessageCount);
        Assert.Equal(0u, destinationState.MessageCount);
    }

    [Fact]
    public async Task Execute_CopiesBodyHeadersAndProperties_AndAcknowledgesOnlyAfterCopy()
    {
        var source = QueueName();
        var destination = QueueName();
        var ledger = Path.Combine(Path.GetTempPath(), $"insight-migration-{Guid.NewGuid():N}.jsonl");
        var payload = Encoding.UTF8.GetBytes("migration-payload");
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(source, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = 1,
            Priority = 3,
            CorrelationId = "correlation-123",
            ReplyTo = "reply-queue",
            MessageId = "message-123",
            Type = "employee.lifecycle",
            AppId = "teampulse-tests",
            Headers = new Dictionary<string, object?> { ["x-contract-version"] = "1" }
        };
        await channel.BasicPublishAsync(string.Empty, source, true, properties, payload);

        var result = await RunUtilityAsync(source, destination, "--execute", "--confirm-quiesced", "--ledger", ledger);

        Assert.Equal(0, result.ExitCode);
        var sourceState = await channel.QueueDeclarePassiveAsync(source);
        var destinationState = await channel.QueueDeclarePassiveAsync(destination);
        Assert.Equal(0u, sourceState.MessageCount);
        Assert.Equal(1u, destinationState.MessageCount);

        var copied = await channel.BasicGetAsync(destination, autoAck: true);
        Assert.NotNull(copied);
        Assert.Equal(payload, copied!.Body.ToArray());
        Assert.Equal("application/json", copied.BasicProperties.ContentType);
        Assert.Equal("utf-8", copied.BasicProperties.ContentEncoding);
        Assert.Equal((byte)2, copied.BasicProperties.DeliveryMode);
        Assert.Equal((byte)3, copied.BasicProperties.Priority);
        Assert.Equal("correlation-123", copied.BasicProperties.CorrelationId);
        Assert.Equal("reply-queue", copied.BasicProperties.ReplyTo);
        Assert.Equal("message-123", copied.BasicProperties.MessageId);
        Assert.Equal("employee.lifecycle", copied.BasicProperties.Type);
        Assert.Equal("teampulse-tests", copied.BasicProperties.AppId);
        Assert.Equal("1", copied.BasicProperties.Headers!["x-contract-version"]);

        var ledgerLines = await File.ReadAllLinesAsync(ledger);
        Assert.Equal(2, ledgerLines.Length);
        Assert.Contains(Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), ledgerLines[0]);
        Assert.DoesNotContain("migration-payload", string.Join(Environment.NewLine, ledgerLines));
        Assert.Contains("copy-summary", ledgerLines[1]);
        File.Delete(ledger);
    }

    [Fact]
    public async Task Execute_WithMessageLimit_CanResumeWithoutLosingSourceMessages()
    {
        var source = QueueName();
        var destination = QueueName();
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(source, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false);

        for (var index = 0; index < 3; index++)
        {
            await PublishAsync(channel, source, $"message-{index}");
        }

        var first = await RunUtilityAsync(source, destination, "--execute", "--confirm-quiesced", "--max-messages", "2");
        Assert.Equal(0, first.ExitCode);
        Assert.Contains("acknowledged: 2", first.Output);
        Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(source)).MessageCount);
        Assert.Equal(2u, (await channel.QueueDeclarePassiveAsync(destination)).MessageCount);

        var second = await RunUtilityAsync(source, destination, "--execute", "--confirm-quiesced", "--max-messages", "2");
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(source)).MessageCount);
        Assert.Equal(3u, (await channel.QueueDeclarePassiveAsync(destination)).MessageCount);
    }

    [Fact]
    public async Task Execute_RefusesQueuesWithActiveConsumers()
    {
        var source = QueueName();
        var destination = QueueName();
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(source, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false);
        var consumer = new AsyncEventingBasicConsumer(channel);
        await channel.BasicConsumeAsync(source, autoAck: false, consumer);

        var result = await RunUtilityAsync(source, destination, "--execute", "--confirm-quiesced");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("active consumers", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_WhenLedgerAppendFailsAfterConfirmedPublish_LeavesSourceUnackedAndRetryCanDuplicate()
    {
        var source = QueueName();
        var destination = QueueName();
        var ledgerDirectory = Path.Combine(Path.GetTempPath(), $"insight-migration-ledger-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ledgerDirectory);
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(source, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false);
        await PublishAsync(channel, source, "retry-window-message");

        // A directory is intentionally supplied as the ledger file. The destination publish is
        // confirmed first; the subsequent ledger append fails, so the utility must close its
        // channel without acknowledging the source delivery.
        var failed = await RunUtilityAsync(
            source,
            destination,
            "--execute",
            "--confirm-quiesced",
            "--ledger",
            ledgerDirectory);

        Assert.Equal(1, failed.ExitCode);
        Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(source)).MessageCount);
        Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(destination)).MessageCount);

        var retryLedger = Path.Combine(Path.GetTempPath(), $"insight-migration-retry-{Guid.NewGuid():N}.jsonl");
        var retried = await RunUtilityAsync(
            source,
            destination,
            "--execute",
            "--confirm-quiesced",
            "--ledger",
            retryLedger);

        Assert.Equal(0, retried.ExitCode);
        Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(source)).MessageCount);
        Assert.Equal(2u, (await channel.QueueDeclarePassiveAsync(destination)).MessageCount);

        var firstCopy = await channel.BasicGetAsync(destination, autoAck: true);
        var secondCopy = await channel.BasicGetAsync(destination, autoAck: true);
        Assert.NotNull(firstCopy);
        Assert.NotNull(secondCopy);
        Assert.Equal(firstCopy!.Body.ToArray(), secondCopy!.Body.ToArray());
        Assert.True(File.Exists(retryLedger));
        File.Delete(retryLedger);
        Directory.Delete(ledgerDirectory);
    }

    [Fact]
    public async Task Execute_RefusesQueuesWithActiveDestinationConsumers()
    {
        var source = QueueName();
        var destination = QueueName();
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(source, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueDeclareAsync(destination, durable: true, exclusive: false, autoDelete: false);
        var consumer = new AsyncEventingBasicConsumer(channel);
        await channel.BasicConsumeAsync(destination, autoAck: false, consumer);

        var result = await RunUtilityAsync(source, destination, "--execute", "--confirm-quiesced");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("active consumers", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(source)).MessageCount);
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(BrokerUri) };
        return await factory.CreateConnectionAsync();
    }

    private static async Task PublishAsync(IChannel channel, string queue, string value)
    {
        await channel.BasicPublishAsync(
            string.Empty,
            queue,
            true,
            new BasicProperties { DeliveryMode = 2 },
            Encoding.UTF8.GetBytes(value));
    }

    private async Task<ProcessResult> RunUtilityAsync(string source, string destination, params string[] additionalArguments)
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var repositoryRoot = FindRepositoryRoot();
        var assemblyPath = Path.Combine(
            repositoryRoot,
            "backend",
            "src",
            "Tools",
            "Insight.QueueMigration",
            "bin",
            configuration,
            "net8.0",
            "Insight.QueueMigration.dll");
        Assert.True(File.Exists(assemblyPath), $"Migration utility was not built: {assemblyPath}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(source);
        startInfo.ArgumentList.Add("--destination");
        startInfo.ArgumentList.Add(destination);
        foreach (var argument in additionalArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["RABBITMQ_URI"] = BrokerUri;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start migration utility.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, (await stdout) + (await stderr));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "TeamPulse.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate backend/TeamPulse.sln from the test output directory.");
    }

    private static string QueueName() => $"insight-migration-test-{Guid.NewGuid():N}";

    private sealed record ProcessResult(int ExitCode, string Output);
}
