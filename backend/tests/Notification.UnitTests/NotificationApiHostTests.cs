using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Persistence;
using Xunit;
namespace Notification.UnitTests;
public sealed class NotificationApiHostTests(NotificationMySqlFixture fixture) : IClassFixture<NotificationMySqlFixture>
{
    [Fact]
    public async Task HostStartup_AppliesMigration_AndSeparatesLivenessFromDatabaseReadiness()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:NotificationDatabase"] = fixture.ConnectionString
                }));
        });

        using var client = factory.CreateClient();
        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NotificationDbContext>>();
        await using var context = await database.CreateDbContextAsync();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
    }
}