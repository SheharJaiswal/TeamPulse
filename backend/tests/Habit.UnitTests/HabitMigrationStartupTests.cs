using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Habit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Habit.UnitTests;

public sealed class HabitMigrationStartupTests(HabitMySqlFixture fixture)
    : IClassFixture<HabitMySqlFixture>
{
    [Fact]
    public async Task Startup_AppliesMigrations_AndRepeatedStartupIsSafe()
    {
        using (var firstFactory = fixture.CreateFactory())
        using (var firstClient = firstFactory.CreateClient())
        {
            var response = await firstClient.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using var scope = firstFactory.Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
            await using var database = await factory.CreateDbContextAsync();
            var applied = (await database.Database.GetAppliedMigrationsAsync()).ToArray();

            Assert.Contains("20261120170000_InitialHabitProjection", applied);
            Assert.Contains("20261121150000_AddHabitDefinitions", applied);
            Assert.True(await database.Database.CanConnectAsync());
            Assert.Empty(await database.EmployeeStatusProjections.ToListAsync());
            Assert.Empty(await database.ProcessedDomainEvents.ToListAsync());
            Assert.Empty(await database.Habits.ToListAsync());
        }

        using (var secondFactory = fixture.CreateFactory())
        using (var secondClient = secondFactory.CreateClient())
        {
            var response = await secondClient.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using var scope = secondFactory.Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
            await using var database = await factory.CreateDbContextAsync();
            var applied = (await database.Database.GetAppliedMigrationsAsync()).ToArray();

            Assert.Equal(1, applied.Count(x => x == "20261120170000_InitialHabitProjection"));
            Assert.Equal(1, applied.Count(x => x == "20261121150000_AddHabitDefinitions"));
        }
    }
}
