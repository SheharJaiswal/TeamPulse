using System.Security.Cryptography;
using System.Text.Json;
using RabbitMQ.Client;

const string usage = """
Insight RabbitMQ queue migration copy utility

Required:
  --source <queue>
  --destination <queue>
  RABBITMQ_URI environment variable

Safe default is preflight only. Execution requires both:
  --execute
  --confirm-quiesced

Optional:
  --ledger <path>          Append SHA-256 payload digests (default: insight-migration-ledger.jsonl)
  --max-messages <count>   Copy at most this many messages per invocation

The utility never declares, deletes, purges or reconfigures queues.
""";

if (args.Contains("--help", StringComparer.Ordinal))
{
    Console.WriteLine(usage);
    return 0;
}

var parsed = ParseArguments(args);
if (parsed.Error is not null)
{
    Console.Error.WriteLine(parsed.Error);
    Console.Error.WriteLine(usage);
    return 2;
}

var options = parsed.Options!;
var rabbitUri = Environment.GetEnvironmentVariable("RABBITMQ_URI");
if (!Uri.TryCreate(rabbitUri, UriKind.Absolute, out var brokerUri) ||
    (brokerUri.Scheme != "amqp" && brokerUri.Scheme != "amqps"))
{
    Console.Error.WriteLine("RABBITMQ_URI must contain a valid amqp:// or amqps:// URI.");
    return 2;
}

if (string.Equals(options.Source, options.Destination, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Source and destination queues must be different.");
    return 2;
}

if (options.Execute && !options.ConfirmQuiesced)
{
    Console.Error.WriteLine("Execution requires --confirm-quiesced after publishers and consumers have been stopped.");
    return 2;
}

if (!options.Execute && options.ConfirmQuiesced)
{
    Console.Error.WriteLine("--confirm-quiesced is valid only together with --execute.");
    return 2;
}

if (options.MaxMessages is <= 0)
{
    Console.Error.WriteLine("--max-messages must be greater than zero.");
    return 2;
}

try
{
    var factory = new ConnectionFactory
    {
        Uri = brokerUri,
        AutomaticRecoveryEnabled = false,
        TopologyRecoveryEnabled = false
    };

    await using var connection = await factory.CreateConnectionAsync();
    var channelOptions = new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    await using var channel = await connection.CreateChannelAsync(channelOptions);

    var source = await channel.QueueDeclarePassiveAsync(options.Source);
    var destination = await channel.QueueDeclarePassiveAsync(options.Destination);

    Console.WriteLine($"Source queue: {options.Source}");
    Console.WriteLine($"Destination queue: {options.Destination}");
    Console.WriteLine($"Source ready messages: {source.MessageCount}");
    Console.WriteLine($"Source consumers: {source.ConsumerCount}");
    Console.WriteLine($"Destination ready messages: {destination.MessageCount}");
    Console.WriteLine($"Mode: {(options.Execute ? "EXECUTE" : "PREFLIGHT ONLY")}");

    if (source.ConsumerCount != 0)
    {
        Console.Error.WriteLine("Preflight failed: source queue has active consumers. Stop all consumers before migration.");
        return 3;
    }

    if (destination.ConsumerCount != 0)
    {
        Console.Error.WriteLine("Preflight failed: destination queue has active consumers. Stop all consumers before migration.");
        return 3;
    }

    if (!options.Execute)
    {
        Console.WriteLine("Preflight succeeded. No messages were read or changed.");
        return 0;
    }

    if (source.MessageCount == 0)
    {
        Console.WriteLine("Source queue is empty. Nothing to copy.");
        return 0;
    }

    var copied = 0;
    var mandatoryPublisher = new Insight.QueueMigration.MandatoryPublisher(channel);

    var ledgerPath = Path.GetFullPath(options.LedgerPath);
    var ledgerDirectory = Path.GetDirectoryName(ledgerPath);
    if (!string.IsNullOrEmpty(ledgerDirectory))
    {
        Directory.CreateDirectory(ledgerDirectory);
    }

    while (options.MaxMessages is null || copied < options.MaxMessages.Value)
    {
        var delivery = await channel.BasicGetAsync(options.Source, autoAck: false);
        if (delivery is null)
        {
            break;
        }

        try
        {
            var properties = CopyProperties(delivery.BasicProperties);
            // The default exchange routes directly to the already-existing destination queue.
            // The helper awaits publisher confirmation and rejects mandatory returns before source ack.
            await mandatoryPublisher.PublishAsync(
                options.Destination,
                properties,
                delivery.Body);

            var digest = Convert.ToHexString(SHA256.HashData(delivery.Body.Span)).ToLowerInvariant();
            var ledgerRecord = JsonSerializer.Serialize(new { sha256 = digest });
            await File.AppendAllTextAsync(ledgerPath, ledgerRecord + Environment.NewLine);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
            copied++;
            Console.WriteLine($"Confirmed and acknowledged message {copied}.");
        }
        catch
        {
            // Never acknowledge a source message unless publish confirmation and ledger append succeeded.
            // Closing the channel leaves an unacknowledged delivery available for redelivery.
            try
            {
                await channel.CloseAsync();
            }
            catch
            {
                // Preserve the original failure; channel disposal will also release the delivery.
            }

            throw;
        }
    }

    var summaryRecord = JsonSerializer.Serialize(new
    {
        type = "copy-summary",
        source = options.Source,
        destination = options.Destination,
        acknowledgedThisRun = copied,
        completedAtUtc = DateTimeOffset.UtcNow
    });
    await File.AppendAllTextAsync(ledgerPath, summaryRecord + Environment.NewLine);

    Console.WriteLine($"Copy completed for this invocation. Messages confirmed and acknowledged: {copied}.");
    Console.WriteLine($"Digest ledger: {ledgerPath}");
    Console.WriteLine("Duplicates are possible after a crash between destination confirmation and source acknowledgement.");
    return 0;
}
catch (Exception)
{
    Console.Error.WriteLine("Migration copy failed. The source delivery was not acknowledged unless its destination publish was confirmed.");
    Console.Error.WriteLine("Inspect broker queue counts and the digest ledger before resuming. No queue was deleted or reconfigured.");
    return 1;
}

static BasicProperties CopyProperties(IReadOnlyBasicProperties source)
{
    var target = new BasicProperties
    {
        ContentType = source.ContentType,
        ContentEncoding = source.ContentEncoding,
        // Queue migration always republishes durably; all other supported message properties are preserved.
        DeliveryMode = DeliveryModes.Persistent,
        Priority = source.Priority,
        CorrelationId = source.CorrelationId,
        ReplyTo = source.ReplyTo,
        Expiration = source.Expiration,
        MessageId = source.MessageId,
        Timestamp = source.Timestamp,
        Type = source.Type,
        UserId = source.UserId,
        AppId = source.AppId,
        ClusterId = source.ClusterId,
        Headers = source.Headers is null
            ? null
            : new Dictionary<string, object?>(source.Headers)
    };

    return target;
}

static (MigrationOptions? Options, string? Error) ParseArguments(string[] arguments)
{
    string? source = null;
    string? destination = null;
    string? ledger = null;
    int? maxMessages = null;
    var execute = false;
    var confirmQuiesced = false;

    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        switch (argument)
        {
            case "--source":
                source = ReadValue(arguments, ref index);
                break;
            case "--destination":
                destination = ReadValue(arguments, ref index);
                break;
            case "--ledger":
                ledger = ReadValue(arguments, ref index);
                break;
            case "--max-messages":
                var rawCount = ReadValue(arguments, ref index);
                if (!int.TryParse(rawCount, out var count))
                {
                    return (null, "--max-messages must be a valid integer.");
                }

                maxMessages = count;
                break;
            case "--execute":
                execute = true;
                break;
            case "--confirm-quiesced":
                confirmQuiesced = true;
                break;
            default:
                return (null, $"Unknown argument: {argument}");
        }
    }

    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
    {
        return (null, "--source and --destination are required.");
    }

    if (source.Length > 255 || destination.Length > 255)
    {
        return (null, "Queue names must not exceed 255 characters.");
    }

    return (new MigrationOptions(
        source,
        destination,
        ledger ?? "insight-migration-ledger.jsonl",
        maxMessages,
        execute,
        confirmQuiesced), null);
}

static string ReadValue(string[] arguments, ref int index)
{
    if (index + 1 >= arguments.Length)
    {
        index = arguments.Length;
        return string.Empty;
    }

    if (arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        return string.Empty;
    }

    index++;
    return arguments[index];
}

internal sealed record MigrationOptions(
    string Source,
    string Destination,
    string LedgerPath,
    int? MaxMessages,
    bool Execute,
    bool ConfirmQuiesced);
