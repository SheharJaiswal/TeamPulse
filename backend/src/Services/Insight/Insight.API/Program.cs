using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Insight.API.HealthChecks;
using Insight.Application;
using Insight.Application.Abstractions;
using Insight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
var dynamoOptions = builder.Configuration.GetSection(InsightDynamoDbOptions.SectionName)
    .Get<InsightDynamoDbOptions>() ?? new InsightDynamoDbOptions();
dynamoOptions.Validate();

if (dynamoOptions.InitializeLocalTable && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Insight DynamoDB table initialization is permitted only in Development.");

builder.Services.AddSingleton(dynamoOptions);
builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
{
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(dynamoOptions.Region)
    };
    if (!string.IsNullOrWhiteSpace(dynamoOptions.ServiceUrl))
    {
        config.ServiceURL = dynamoOptions.ServiceUrl;
        return new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"), config);
    }

    return new AmazonDynamoDBClient(config);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEmployeeStatusProjectionStore, DynamoDbEmployeeStatusProjectionStore>();
builder.Services.AddSingleton<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
builder.Services.AddSingleton<InsightDynamoDbTableInitializer>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<InsightDynamoDbHealthCheck>("insight-dynamodb", tags: new[] { "ready" })
    .AddCheck<InsightConsumerHealthCheck>("insight-consumer", tags: new[] { "consumer" });

var app = builder.Build();

if (dynamoOptions.InitializeLocalTable)
{
    await app.Services.GetRequiredService<InsightDynamoDbTableInitializer>()
        .InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/consumer", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("consumer")
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();

public partial class Program { }