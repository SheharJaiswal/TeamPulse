using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Insight.UnitTests;

public sealed class DynamoDbLocalFixture : IAsyncLifetime
{
    private readonly TestcontainersContainer _container = new TestcontainersBuilder<TestcontainersContainer>()
        .WithImage("amazon/dynamodb-local:2.5.2")
        .WithPortBinding(8000, true)
        .WithCommand("-inMemory", "-sharedDb")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(8000))
        .Build();

    public string ServiceUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8000)}";

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public IAmazonDynamoDB CreateClient() => new AmazonDynamoDBClient(
        new BasicAWSCredentials("local", "local"),
        new AmazonDynamoDBConfig
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = "us-east-1"
        });
}