using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeLifecycleAudit> EmployeeLifecycleAudits => Set<EmployeeLifecycleAudit>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var employee = modelBuilder.Entity<Employee>();
        employee.ToTable("employees");
        employee.HasKey(x => x.Id);
        employee.Property(x => x.Email).HasMaxLength(320).IsRequired();
        employee.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        employee.HasIndex(x => x.NormalizedEmail).IsUnique();
        employee.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        employee.Property(x => x.PasswordHash).IsRequired();
        employee.Property(x => x.OrganizationId).HasMaxLength(100).IsRequired();
        employee.Property(x => x.TeamId).HasMaxLength(100).IsRequired();
        employee.Property(x => x.Role).HasMaxLength(50).IsRequired();
        employee.Property(x => x.LifecycleVersion).HasDefaultValue(0L).IsConcurrencyToken();

        var audit = modelBuilder.Entity<EmployeeLifecycleAudit>();
        audit.ToTable("employee_lifecycle_audits");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Source).HasMaxLength(50).IsRequired();
        audit.Property(x => x.PreviousStatus).HasMaxLength(20).IsRequired();
        audit.Property(x => x.NewStatus).HasMaxLength(20).IsRequired();
        audit.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        audit.Property(x => x.Note).HasMaxLength(1000);
        audit.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        audit.Property(x => x.RequestFingerprint).HasColumnType("char(64)").IsRequired();
        audit.HasIndex(x => new { x.ActorEmployeeId, x.IdempotencyKey }).IsUnique();
        audit.HasIndex(x => new { x.EmployeeId, x.LifecycleVersion }).IsUnique();

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("outbox_messages");
        outbox.HasKey(x => x.EventId);
        outbox.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        outbox.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
        outbox.HasIndex(x => new { x.PublishedAtUtc, x.CreatedAtUtc });
    }
}
