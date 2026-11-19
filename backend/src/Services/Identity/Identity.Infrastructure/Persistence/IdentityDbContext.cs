using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Identity.Infrastructure.Persistence;
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e=modelBuilder.Entity<Employee>();
        e.ToTable("employees"); e.HasKey(x=>x.Id);
        e.Property(x=>x.Email).HasMaxLength(320).IsRequired();
        e.Property(x=>x.NormalizedEmail).HasMaxLength(320).IsRequired();
        e.HasIndex(x=>x.NormalizedEmail).IsUnique();
        e.Property(x=>x.DisplayName).HasMaxLength(150).IsRequired();
        e.Property(x=>x.PasswordHash).IsRequired();
        e.Property(x=>x.OrganizationId).HasMaxLength(100).IsRequired();
        e.Property(x=>x.TeamId).HasMaxLength(100).IsRequired();
        e.Property(x=>x.Role).HasMaxLength(50).IsRequired();
    }
}