using Identity.AdminBootstrap;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

if (args.Length != 1 || !string.Equals(args[0], "--execute", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Run explicitly with --execute. The password must be supplied through protected environment configuration.");
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

if (!BootstrapOptions.TryLoad(configuration, out var bootstrapOptions, out var validationError))
{
    Console.Error.WriteLine(validationError);
    return 2;
}

var connectionString = configuration.GetConnectionString("IdentityDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("The Identity database connection string is required.");
    return 2;
}

try
{
    var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
        .Options;

    await using var database = new IdentityDbContext(dbOptions);
    var bootstrapper = new HrAdminBootstrapper(
        database,
        new PasswordHasher<Employee>());

    var result = await bootstrapper.RunAsync(bootstrapOptions!);
    switch (result)
    {
        case BootstrapResult.Created:
            Console.WriteLine("The initial HR administrator was created.");
            return 0;
        case BootstrapResult.AdministratorAlreadyExists:
            Console.Error.WriteLine("An HR administrator already exists. No changes were made.");
            return 3;
        case BootstrapResult.EmailAlreadyExists:
            Console.Error.WriteLine("The configured email already belongs to an employee. No changes were made.");
            return 3;
        default:
            Console.Error.WriteLine("The bootstrap lock could not be acquired. No changes were made.");
            return 4;
    }
}
catch
{
    Console.Error.WriteLine("HR administrator bootstrap failed. Check protected operator logs; no credentials are printed.");
    return 1;
}
