using Identity.AdminBootstrap;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.UnitTests;

public sealed class HrAdminBootstrapperIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public HrAdminBootstrapperIntegrationTests(MySqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_CreatesOneActiveHrAdminWithHashedPassword()
    {
        await ResetDatabaseAsync();
        var options = CreateOptions();

        var result = await RunAsync(options);

        Assert.Equal(BootstrapResult.Created, result);
        await using var database = CreateDatabase();
        var employee = await database.Employees.SingleAsync();
        Assert.Equal(HrAdminBootstrapper.HrAdminRole, employee.Role);
        Assert.True(employee.IsActive);
        Assert.NotEqual(options.Password, employee.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<Employee>().VerifyHashedPassword(
                employee, employee.PasswordHash, options.Password));
    }

    [Fact]
    public async Task RunAsync_RefusesWhenHrAdminAlreadyExistsWithoutMutation()
    {
        await ResetDatabaseAsync();
        var first = await RunAsync(CreateOptions());
        Assert.Equal(BootstrapResult.Created, first);

        var second = await RunAsync(CreateOptions("another.hr@teampulse.test"));

        Assert.Equal(BootstrapResult.AdministratorAlreadyExists, second);
        await using var database = CreateDatabase();
        Assert.Equal(1, await database.Employees.CountAsync());
    }

    [Fact]
    public async Task RunAsync_RefusesWhenInactiveHrAdminAlreadyExistsWithoutMutation()
    {
        await ResetDatabaseAsync();
        await using (var database = CreateDatabase())
        {
            var employee = new Employee(
                Guid.NewGuid(), "former.hr@teampulse.test", "FORMER.HR@TEAMPULSE.TEST",
                "Former HR Administrator", "already-hashed", "org-test", "team-test", "HrAdmin");
            database.Employees.Add(employee);
            await database.SaveChangesAsync();
            await database.Employees
                .Where(candidate => candidate.Id == employee.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.IsActive, false));
        }

        var result = await RunAsync(CreateOptions());

        Assert.Equal(BootstrapResult.AdministratorAlreadyExists, result);
        await using var verify = CreateDatabase();
        var stored = await verify.Employees.SingleAsync();
        Assert.Equal("HrAdmin", stored.Role);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task RunAsync_RefusesWhenConfiguredEmailAlreadyExistsWithoutMutation()
    {
        await ResetDatabaseAsync();
        await using (var database = CreateDatabase())
        {
            var employee = new Employee(
                Guid.NewGuid(), "existing@teampulse.test", "EXISTING@TEAMPULSE.TEST",
                "Existing Employee", "already-hashed", "org-test", "team-test", "Employee");
            database.Employees.Add(employee);
            await database.SaveChangesAsync();
        }

        var result = await RunAsync(CreateOptions("existing@teampulse.test"));

        Assert.Equal(BootstrapResult.EmailAlreadyExists, result);
        await using var verify = CreateDatabase();
        Assert.Equal(1, await verify.Employees.CountAsync());
        Assert.Equal("Employee", (await verify.Employees.SingleAsync()).Role);
    }

    [Fact]
    public async Task RunAsync_ConcurrentInvocations_CreateAtMostOneHrAdmin()
    {
        await ResetDatabaseAsync();
        var first = RunAsync(CreateOptions());
        var second = RunAsync(CreateOptions("second.hr@teampulse.test"));

        var results = await Task.WhenAll(first, second);

        Assert.Single(results.Where(result => result == BootstrapResult.Created));
        Assert.Single(results.Where(result => result == BootstrapResult.AdministratorAlreadyExists));

        await using var database = CreateDatabase();
        Assert.Equal(1, await database.Employees.CountAsync(
            employee => employee.Role == HrAdminBootstrapper.HrAdminRole));
    }

    private async Task ResetDatabaseAsync()
    {
        await using var database = CreateDatabase();
        await database.Database.MigrateAsync();
        await database.Employees.ExecuteDeleteAsync();
    }

    private async Task<BootstrapResult> RunAsync(BootstrapOptions options)
    {
        await using var database = CreateDatabase();
        return await new HrAdminBootstrapper(
            database, new PasswordHasher<Employee>()).RunAsync(options);
    }

    private IdentityDbContext CreateDatabase()
    {
        var connectionString = _fixture.ConnectionString;
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;
        return new IdentityDbContext(options);
    }

    private static BootstrapOptions CreateOptions(string email = "hr.admin@teampulse.test") =>
        new(email, "TeamPulse HR Administrator", "org-test", "team-test",
            "Strong-HR-Admin-Password-123!");
}
