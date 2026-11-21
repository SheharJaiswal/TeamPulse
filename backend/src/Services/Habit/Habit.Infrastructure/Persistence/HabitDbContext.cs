using Habit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Habit.Infrastructure.Persistence;

public sealed class HabitDbContext(DbContextOptions<HabitDbContext> options) : DbContext(options)
{
    public DbSet<EmployeeStatusProjection> EmployeeStatusProjections => Set<EmployeeStatusProjection>();
    public DbSet<ProcessedDomainEvent> ProcessedDomainEvents => Set<ProcessedDomainEvent>();
    public DbSet<HabitDefinition> Habits => Set<HabitDefinition>();

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
        inbox.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)");
        inbox.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint");
        inbox.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("datetime(6)");
        inbox.HasIndex(x => new { x.EmployeeId, x.LifecycleVersion })
            .HasDatabaseName("IX_processed_domain_events_employee_version");

        var habit = modelBuilder.Entity<HabitDefinition>();
        habit.ToTable("habits");
        habit.HasKey(x => x.Id);
        habit.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        habit.Property(x => x.OwnerEmployeeId).HasColumnName("owner_employee_id").HasColumnType("char(36)").IsRequired();
        habit.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        habit.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        habit.Property(x => x.Category).HasColumnName("category").HasMaxLength(50);
        habit.Property(x => x.Frequency).HasColumnName("frequency").HasConversion<int>().IsRequired();
        habit.Property(x => x.TargetCount).HasColumnName("target_count").IsRequired();
        habit.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        habit.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date");
        habit.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        habit.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
        habit.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();
        habit.Property(x => x.ArchivedAtUtc).HasColumnName("archived_at_utc").HasColumnType("datetime(6)");
        habit.HasIndex(x => new { x.OwnerEmployeeId, x.CreatedAtUtc });
        habit.HasIndex(x => new { x.OwnerEmployeeId, x.Status });
    }
}
