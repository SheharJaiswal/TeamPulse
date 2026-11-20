using Identity.API.Controllers;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.UnitTests;

public sealed class IdentityControllerTests
{
    private const string ValidPassword = "Strong-Test-Password-123!";
    private const string TestToken = "unit-test-access-token";

    [Theory]
    [InlineData("", ValidPassword, "Employee", "org-1", "team-1")]
    [InlineData("employee@example.test", "", "Employee", "org-1", "team-1")]
    [InlineData("employee@example.test", ValidPassword, "", "org-1", "team-1")]
    [InlineData("employee@example.test", ValidPassword, "Employee", "", "team-1")]
    [InlineData("employee@example.test", ValidPassword, "Employee", "org-1", "")]
    [InlineData("   ", ValidPassword, "Employee", "org-1", "team-1")]
    public async Task Register_WhenAnyRequiredFieldIsMissing_ReturnsBadRequestWithoutMutation(
        string email, string password, string displayName, string organizationId, string teamId)
    {
        var repository = new FakeEmployeeRepository();
        var controller = CreateController(repository);

        var result = await controller.Register(
            new RegisterRequest(email, password, displayName, organizationId, teamId),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(repository.Employees);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Register_WhenPasswordIsShorterThanTwelveCharacters_ReturnsBadRequestWithoutMutation()
    {
        var repository = new FakeEmployeeRepository();
        var controller = CreateController(repository);

        var result = await controller.Register(
            new RegisterRequest("employee@example.test", "short-pass", "Employee", "org-1", "team-1"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(repository.Employees);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Register_WhenNormalizedEmailAlreadyExists_ReturnsConflictWithoutMutation()
    {
        var repository = new FakeEmployeeRepository();
        repository.Seed(CreateEmployee("EMPLOYEE@EXAMPLE.TEST", "existing-hash"));
        var controller = CreateController(repository);

        var result = await controller.Register(
            new RegisterRequest(" employee@example.test ", ValidPassword, "Employee", "org-1", "team-1"),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(repository.Employees);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Register_WhenRequestIsValid_TrimsFieldsAndPersistsOnlyPasswordHash()
    {
        var repository = new FakeEmployeeRepository();
        var hasher = new PasswordHasher<Employee>();
        var controller = CreateController(repository, hasher);

        var result = await controller.Register(
            new RegisterRequest(" employee@example.test ", ValidPassword, " Employee One ", " org-1 ", " team-1 "),
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var auth = Assert.IsType<AuthResponse>(response.Value);
        var saved = Assert.Single(repository.Employees);

        Assert.Equal("employee@example.test", saved.Email);
        Assert.Equal("EMPLOYEE@EXAMPLE.TEST", saved.NormalizedEmail);
        Assert.Equal("Employee One", saved.DisplayName);
        Assert.Equal("org-1", saved.OrganizationId);
        Assert.Equal("team-1", saved.TeamId);
        Assert.NotEqual(ValidPassword, saved.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(saved, saved.PasswordHash, ValidPassword));
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Equal(TestToken, auth.AccessToken);
        Assert.Equal("Bearer", auth.TokenType);
        Assert.Equal(saved.Id, auth.Employee.EmployeeId);
    }

    [Fact]
    public async Task Login_WhenEmailDoesNotExist_ReturnsGenericUnauthorizedResponse()
    {
        var repository = new FakeEmployeeRepository();
        var controller = CreateController(repository);

        var result = await controller.Login(
            new LoginRequest("missing@example.test", ValidPassword),
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Invalid email or password.", GetMessage(unauthorized));
    }

    [Fact]
    public async Task Login_WhenPasswordIsIncorrect_ReturnsSameGenericUnauthorizedResponse()
    {
        var repository = new FakeEmployeeRepository();
        var hasher = new PasswordHasher<Employee>();
        var employee = CreateEmployee("employee@example.test", "placeholder");
        employee = employee.WithPasswordHash(hasher.HashPassword(employee, ValidPassword));
        repository.Seed(employee);
        var controller = CreateController(repository, hasher);

        var result = await controller.Login(
            new LoginRequest("employee@example.test", "Wrong-Password-123!"),
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Invalid email or password.", GetMessage(unauthorized));
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_NormalizesEmailAndReturnsToken()
    {
        var repository = new FakeEmployeeRepository();
        var hasher = new PasswordHasher<Employee>();
        var employee = CreateEmployee("employee@example.test", "placeholder");
        employee = employee.WithPasswordHash(hasher.HashPassword(employee, ValidPassword));
        repository.Seed(employee);
        var controller = CreateController(repository, hasher);

        var result = await controller.Login(
            new LoginRequest(" EMPLOYEE@EXAMPLE.TEST ", ValidPassword),
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var auth = Assert.IsType<AuthResponse>(response.Value);
        Assert.Equal(TestToken, auth.AccessToken);
        Assert.Equal("Bearer", auth.TokenType);
        Assert.Equal(employee.Id, auth.Employee.EmployeeId);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Register_WhenEnvironmentIsNotDevelopment_ReturnsNotFoundWithoutMutation(
        string environmentName)
    {
        var repository = new FakeEmployeeRepository();
        var controller = CreateController(repository, environmentName: environmentName);

        var result = await controller.Register(
            new RegisterRequest("employee@example.test", ValidPassword, "Employee", "org-1", "team-1"),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(repository.Employees);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    private static IdentityController CreateController(
        FakeEmployeeRepository repository,
        PasswordHasher<Employee>? hasher = null,
        string environmentName = "Development")
    {
        return new IdentityController(
            repository,
            hasher ?? new PasswordHasher<Employee>(),
            new FakeTokenService(),
            new FakeHostEnvironment(environmentName),
            Options.Create(new JwtOptions
            {
                Issuer = "TeamPulse.Identity.Tests",
                Audience = "TeamPulse.Services.Tests",
                SigningKey = "unit-test-signing-key-with-at-least-32-bytes",
                AccessTokenMinutes = 15
            }));
    }

    private static Employee CreateEmployee(string email, string passwordHash)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return new Employee(
            Guid.NewGuid(), email, normalized, "Test Employee", passwordHash,
            "org-1", "team-1", "Employee");
    }

    private static string GetMessage(ObjectResult result)
    {
        var message = result.Value?.GetType().GetProperty("message")?.GetValue(result.Value);
        return Assert.IsType<string>(message);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Identity.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeEmployeeRepository : IEmployeeRepository
    {
        public List<Employee> Employees { get; } = [];
        public int AddCalls { get; private set; }
        public int SaveChangesCalls { get; private set; }

        public void Seed(Employee employee) => Employees.Add(employee);

        public Task<Employee?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            return Task.FromResult(Employees.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
        }

        public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Employees.SingleOrDefault(x => x.Id == id));
        }

        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            AddCalls++;
            Employees.Add(employee);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateAccessToken(Employee employee) => TestToken;
    }
}

internal static class EmployeeTestExtensions
{
    public static Employee WithPasswordHash(this Employee employee, string passwordHash)
    {
        return new Employee(
            employee.Id, employee.Email, employee.NormalizedEmail, employee.DisplayName,
            passwordHash, employee.OrganizationId, employee.TeamId, employee.Role);
    }
}
