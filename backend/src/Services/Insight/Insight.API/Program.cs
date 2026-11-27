using System.Text;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Insight.API.HealthChecks;
using Insight.Application;
using Insight.Application.Abstractions;
using Insight.Infrastructure.Persistence;
using Insight.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var consumerOptions = builder.Configuration.GetSection(InsightRabbitMqConsumerOptions.SectionName)
    .Get<InsightRabbitMqConsumerOptions>() ?? new InsightRabbitMqConsumerOptions();
consumerOptions.Validate();
var activityConsumerOptions = builder.Configuration.GetSection(InsightActivityConsumerOptions.SectionName)
    .Get<InsightActivityConsumerOptions>() ?? new InsightActivityConsumerOptions();
activityConsumerOptions.Validate();

var dynamoOptions = builder.Configuration.GetSection(InsightDynamoDbOptions.SectionName)
    .Get<InsightDynamoDbOptions>() ?? new InsightDynamoDbOptions();
dynamoOptions.Validate();

if (dynamoOptions.InitializeLocalTable && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Insight DynamoDB table initialization is permitted only in Development.");

var activityConnectionString = builder.Configuration.GetConnectionString("InsightActivityDatabase")
    ?? throw new InvalidOperationException("InsightActivityDatabase connection string is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TeamPulse.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TeamPulse.Services";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey must be configured using user secrets or environment variables.");
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");

builder.Services.AddDbContextFactory<InsightActivityDbContext>(options =>
    options.UseMySql(activityConnectionString, ServerVersion.AutoDetect(activityConnectionString)));
builder.Services.AddSingleton(dynamoOptions);
builder.Services.AddSingleton(consumerOptions);
builder.Services.AddSingleton(activityConsumerOptions);
builder.Services.AddHostedService<InsightActivityRabbitMqConsumer>();
builder.Services.AddSingleton<InsightRabbitMqConsumerStatus>();
builder.Services.AddHostedService<InsightRabbitMqConsumer>();
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
builder.Services.AddScoped<IActivitySummaryStore, MySqlActivitySummaryStore>();
builder.Services.AddScoped<ActivityRecordedEventProcessor>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwtIssuer,
            ValidateAudience = true, ValidAudience = jwtAudience,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            NameClaimType = "name", RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter a valid JWT access token. Swagger sends it as: Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<InsightDynamoDbHealthCheck>("insight-dynamodb", tags: new[] { "ready" })
    .AddCheck<InsightActivityDatabaseHealthCheck>("insight-activity-database", tags: new[] { "ready" })
    .AddCheck<InsightConsumerHealthCheck>("insight-consumer", tags: new[] { "consumer" });

var app = builder.Build();

if (dynamoOptions.InitializeLocalTable)
{
    await app.Services.GetRequiredService<InsightDynamoDbTableInitializer>()
        .InitializeAsync(app.Lifetime.ApplicationStopping);
}
await using (var db = await app.Services.GetRequiredService<IDbContextFactory<InsightActivityDbContext>>()
    .CreateDbContextAsync())
{
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/insights/activity/daily",
    async (DateOnly? fromDate, DateOnly? toDate, System.Security.Claims.ClaimsPrincipal user,
        IActivitySummaryStore store, TimeProvider clock, CancellationToken cancellationToken) =>
    {
        var subject = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var employeeId) || employeeId == Guid.Empty)
            return Results.Unauthorized();

        if (fromDate.HasValue != toDate.HasValue)
            return Results.BadRequest(new { error = "fromDate and toDate must be provided together." });

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var from = fromDate ?? today.AddDays(-6);
        var to = toDate ?? today;
        if (from > to || to.DayNumber - from.DayNumber >= 30)
            return Results.BadRequest(new { error = "Date range must be inclusive, ordered, and no longer than 30 days." });

        var days = await store.GetDailySummaryAsync(employeeId, from, to, cancellationToken);
        return Results.Ok(days);
    })
    .RequireAuthorization()
    .WithName("GetMyDailyActivitySummary");

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
