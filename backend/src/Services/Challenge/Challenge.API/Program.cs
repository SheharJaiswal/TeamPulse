using System;
using System.Linq;
using System.Text;
using Challenge.API.HealthChecks;
using Challenge.API.BackgroundServices;
using Challenge.Application;
using Challenge.Application.Abstractions;
using Challenge.Infrastructure.Messaging;
using Challenge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ChallengeDatabase")
    ?? throw new InvalidOperationException("ChallengeDatabase connection string is missing.");
var consumerOptions = builder.Configuration.GetSection(ChallengeRabbitMqConsumerOptions.SectionName)
    .Get<ChallengeRabbitMqConsumerOptions>() ?? new ChallengeRabbitMqConsumerOptions();
consumerOptions.Validate();
var issuer = builder.Configuration["Jwt:Issuer"] ?? "TeamPulse.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "TeamPulse.Services";
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
if (Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Challenge JWT SigningKey must contain at least 32 UTF-8 bytes.");

builder.Services.AddHttpClient();
builder.Services.AddDbContextFactory<ChallengeDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(consumerOptions);
builder.Services.AddSingleton<ChallengeRabbitMqConsumerStatus>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore, EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
builder.Services.AddHostedService<ChallengeRabbitMqConsumer>();
builder.Services.AddHostedService<ChallengeInvitationEmailWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
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

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration("challenge-database",
        sp => new ChallengeDatabaseHealthCheck(sp.GetRequiredService<IDbContextFactory<ChallengeDbContext>>()),
        HealthStatus.Unhealthy, new[] { "ready" }, TimeSpan.FromSeconds(3)))
    .Add(new HealthCheckRegistration("challenge-rabbitmq-consumer",
        sp => new ChallengeRabbitMqConsumerHealthCheck(
            sp.GetRequiredService<ChallengeRabbitMqConsumerStatus>(),
            sp.GetRequiredService<ChallengeRabbitMqConsumerOptions>()),
        HealthStatus.Unhealthy, new[] { "consumer" }, TimeSpan.FromSeconds(3)));

var app = builder.Build();

await using (var database = await app.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
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
