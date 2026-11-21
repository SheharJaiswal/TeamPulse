using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
namespace Notification.Infrastructure.Persistence;
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<EmployeeStatusProjection> EmployeeStatusProjections => Set<EmployeeStatusProjection>();
    public DbSet<ProcessedDomainEvent> ProcessedDomainEvents => Set<ProcessedDomainEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var projection = modelBuilder.Entity<EmployeeStatusProjection>();
        projection.ToTable("employee_status_projections");
        projection.HasKey(x => x.EmployeeId);
        projection.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)");
        projection.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("tinyint(1)").IsRequired();
        projection.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint").IsRequired();
        projection.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();

        var inbox = modelBuilder.Entity<ProcessedDomainEvent>();
        inbox.ToTable("processed_domain_events");
        inbox.HasKey(x => x.EventId);
        inbox.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)");
        inbox.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        inbox.Property(x => x.SchemaVersion).HasColumnName("schema_version").IsRequired();
        inbox.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        inbox.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint").IsRequired();
        inbox.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("datetime(6)").IsRequired();
    }
}