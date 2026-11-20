using System;
using System.Linq;
using System.Text;
using Amazon.EventBridge;
using Identity.API.BackgroundServices;
using Identity.API.HealthChecks;
using Identity.API.Security;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Messaging;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
JwtSigningKeyValidator.Validate(jwt.SigningKey);

var connectionString = builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException("IdentityDatabase connection string is missing.");

builder.Services.AddDbContextFactory<IdentityDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeLifecycleService, EmployeeLifecycleService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Identity.Domain.Entities.Employee>,
    Microsoft.AspNetCore.Identity.PasswordHasher<Identity.Domain.Entities.Employee>>();

var dispatcherOptions = builder.Configuration.GetSection(OutboxDispatcherOptions.SectionName)
    .Get<OutboxDispatcherOptions>() ?? new OutboxDispatcherOptions();
if (dispatcherOptions.Enabled)
{
    dispatcherOptions.ValidateGeneral();
    builder.Services.Configure<OutboxDispatcherOptions>(
        builder.Configuration.GetSection(OutboxDispatcherOptions.SectionName));
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IOutboxMessageStore, OutboxMessageStore>();

    switch (dispatcherOptions.Transport.Trim().ToLowerInvariant())
    {
        case "rabbitmq":
            var rabbitMqOptions = builder.Configuration.GetSection(RabbitMqOptions.SectionName)
                .Get<RabbitMqOptions>() ?? new RabbitMqOptions();
            rabbitMqOptions.Validate();
            builder.Services.AddSingleton(rabbitMqOptions);
            builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
            break;

        case "eventbridge":
            var busName = builder.Configuration["EventBridge:BusName"];
            var awsRegion = builder.Configuration["AWS:Region"] ??
                Environment.GetEnvironmentVariable("AWS_REGION");
            dispatcherOptions.Validate(busName ?? string.Empty, awsRegion ?? string.Empty);
            builder.Services.AddSingleton<IAmazonEventBridge>(_ =>
                new AmazonEventBridgeClient(new AmazonEventBridgeConfig
                {
                    Timeout = TimeSpan.FromSeconds(20)
                }));
            builder.Services.AddSingleton<IEventPublisher>(serviceProvider =>
                new EventBridgePublisher(
                    serviceProvider.GetRequiredService<IAmazonEventBridge>(), busName!));
            break;

        default:
            throw new InvalidOperationException(
                "OutboxDispatcher:Transport must be either RabbitMq or EventBridge.");
    }

    builder.Services.AddHostedService<OutboxDispatcherWorker>();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        NameClaimType = "name",
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration(
        "identity-database",
        serviceProvider => new IdentityDatabaseHealthCheck(
            serviceProvider.GetRequiredService<IServiceScopeFactory>()),
        HealthStatus.Unhealthy,
        new[] { "ready" },
        TimeSpan.FromSeconds(3)));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();

public partial class Program { }
