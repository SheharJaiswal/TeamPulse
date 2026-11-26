using System;
using System.Linq;
using System.Text;
using EventTracking.API.BackgroundServices;
using EventTracking.API.HealthChecks;
using EventTracking.API.Security;
using EventTracking.Application;
using EventTracking.Application.Abstractions;
using EventTracking.Infrastructure.Persistence;
using EventTracking.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var consumerOptions = builder.Configuration.GetSection(EventTrackingRabbitMqConsumerOptions.SectionName)
    .Get<EventTrackingRabbitMqConsumerOptions>() ?? new EventTrackingRabbitMqConsumerOptions();
consumerOptions.Validate();
var publisherOptions = builder.Configuration.GetSection(EventTrackingRabbitMqPublisherOptions.SectionName)
    .Get<EventTrackingRabbitMqPublisherOptions>() ?? new EventTrackingRabbitMqPublisherOptions();
publisherOptions.Validate();
var outboxOptions = builder.Configuration.GetSection(EventTrackingOutboxDispatcherOptions.SectionName)
    .Get<EventTrackingOutboxDispatcherOptions>() ?? new EventTrackingOutboxDispatcherOptions();
outboxOptions.Validate();

var connectionString = builder.Configuration.GetConnectionString("EventTrackingDatabase")
    ?? throw new InvalidOperationException("EventTrackingDatabase connection string is missing.");
var jwtOptions = builder.Configuration.GetSection(EventTrackingJwtOptions.SectionName).Get<EventTrackingJwtOptions>()
    ?? new EventTrackingJwtOptions();
jwtOptions.Validate();

builder.Services.AddDbContextFactory<EventTrackingDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
builder.Services.AddHttpClient("HabitApi", client =>
{
    var baseUrl = builder.Configuration["HabitApi:BaseUrl"] ?? "http://localhost:5102/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true, ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            NameClaimType = "name", RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Enter a valid JWT access token. Swagger sends it as: Authorization: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(consumerOptions);
builder.Services.AddSingleton(publisherOptions);
builder.Services.AddSingleton<IEventTrackingPublisher, EventTrackingRabbitMqPublisher>();
builder.Services.AddSingleton<IOptions<EventTrackingOutboxDispatcherOptions>>(Options.Create(outboxOptions));
builder.Services.AddSingleton<EventTrackingRabbitMqConsumerStatus>();
builder.Services.AddHostedService<EventTrackingRabbitMqConsumer>();
builder.Services.AddHostedService<EventTrackingOutboxDispatcher>();
builder.Services.AddScoped<EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionReader>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
builder.Services.AddScoped<IHabitActivitySubmissionStore, HabitActivitySubmissionStore>();
builder.Services.AddScoped<IEventTrackingOutboxStore, EventTrackingOutboxStore>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration("event-tracking-database",
        sp => new EventTrackingDatabaseHealthCheck(sp.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>()),
        HealthStatus.Unhealthy, new[] { "ready" }, TimeSpan.FromSeconds(3)))
    .Add(new HealthCheckRegistration("event-tracking-rabbitmq-consumer",
        sp => new EventTrackingRabbitMqConsumerHealthCheck(
            sp.GetRequiredService<EventTrackingRabbitMqConsumerStatus>(),
            sp.GetRequiredService<EventTrackingRabbitMqConsumerOptions>()),
        HealthStatus.Unhealthy, new[] { "consumer" }, TimeSpan.FromSeconds(3)));
var app = builder.Build();
await using (var database = await app.Services.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>().CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health/consumer", new HealthCheckOptions { Predicate = check => check.Tags.Contains("consumer") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();
public partial class Program { }
