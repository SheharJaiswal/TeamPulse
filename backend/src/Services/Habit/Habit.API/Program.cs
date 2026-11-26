using System;
using System.Linq;
using System.Text;
using Habit.API.HealthChecks;
using Habit.API.Security;
using Habit.Application;
using Habit.Application.Abstractions;
using Habit.Infrastructure.Messaging;
using Habit.Infrastructure.Persistence;
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

var connectionString = builder.Configuration.GetConnectionString("HabitDatabase")
    ?? throw new InvalidOperationException("HabitDatabase connection string is missing.");

var jwtOptions = builder.Configuration.GetSection(HabitJwtOptions.SectionName).Get<HabitJwtOptions>()
    ?? new HabitJwtOptions();
jwtOptions.Validate();

var habitConsumerOptions = builder.Configuration
    .GetSection(HabitRabbitMqConsumerOptions.SectionName)
    .Get<HabitRabbitMqConsumerOptions>() ?? new HabitRabbitMqConsumerOptions();
habitConsumerOptions.Validate();

builder.Services.AddDbContextFactory<HabitDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(habitConsumerOptions);
builder.Services.AddSingleton<HabitRabbitMqConsumerStatus>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore, EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionReader, EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
builder.Services.AddScoped<IHabitRepository, HabitRepository>();
builder.Services.AddScoped<IHabitService, HabitService>();
builder.Services.AddHostedService<HabitRabbitMqConsumer>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
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
    .Add(new HealthCheckRegistration(
        "habit-database",
        serviceProvider => new HabitDatabaseHealthCheck(
            serviceProvider.GetRequiredService<IDbContextFactory<HabitDbContext>>()),
        HealthStatus.Unhealthy,
        new[] { "ready" },
        TimeSpan.FromSeconds(3)))
    .Add(new HealthCheckRegistration(
        "habit-rabbitmq-consumer",
        serviceProvider => new HabitRabbitMqConsumerHealthCheck(
            serviceProvider.GetRequiredService<HabitRabbitMqConsumerStatus>(),
            serviceProvider.GetRequiredService<HabitRabbitMqConsumerOptions>()),
        HealthStatus.Unhealthy,
        new[] { "consumer" },
        TimeSpan.FromSeconds(3)));

var app = builder.Build();

await using (var database = await app.Services
    .GetRequiredService<IDbContextFactory<HabitDbContext>>()
    .CreateDbContextAsync())
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
