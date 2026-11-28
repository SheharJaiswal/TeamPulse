using System.Text;
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
builder.Services.AddSingleton(consumerOptions);
builder.Services.AddSingleton(activityConsumerOptions);
builder.Services.AddSingleton<InsightActivityConsumerStatus>();
builder.Services.AddHostedService<InsightActivityRabbitMqConsumer>();
builder.Services.AddSingleton<InsightRabbitMqConsumerStatus>();
builder.Services.AddHostedService<InsightRabbitMqConsumer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IEmployeeStatusProjectionStore, MySqlEmployeeStatusProjectionStore>();
builder.Services.AddSingleton<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
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
    .AddCheck<InsightActivityDatabaseHealthCheck>("insight-activity-database", tags: new[] { "ready" })
    .AddCheck<InsightConsumerHealthCheck>("insight-consumer", tags: new[] { "consumer" })
    .AddCheck<InsightActivityConsumerHealthCheck>("insight-activity-consumer", tags: new[] { "activity-consumer" });

var app = builder.Build();

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

app.MapGet("/api/insights/activity/daily", GetMyDailyActivitySummary)
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
app.MapHealthChecks("/health/activity-consumer", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("activity-consumer")
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

static async Task<IResult> GetMyDailyActivitySummary(
    DateOnly? fromDate,
    DateOnly? toDate,
    System.Security.Claims.ClaimsPrincipal user,
    IActivitySummaryStore store,
    TimeProvider clock,
    CancellationToken cancellationToken)
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
}

app.Run();

public partial class Program { }
