using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Xunit;

namespace Insight.UnitTests;

public sealed class DynamoDbLocalFixture : IAsyncLifetime
{
    private readonly IContainer _container = new ContainerBuilder()
        .WithImage("amazon/dynamodb-local:2.5.2")
        .WithImagePullPolicy(PullPolicy.Always)
        .WithPortBinding(8000, true)
        .WithCommand("-jar", "DynamoDBLocal.jar", "-inMemory", "-sharedDb")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8000))
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