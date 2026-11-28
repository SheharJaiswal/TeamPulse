using Insight.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Insight.Infrastructure.Persistence;

public sealed class InsightActivityDbContext(DbContextOptions<InsightActivityDbContext> options)
    : DbContext(options)
{
    public DbSet<InsightDailyActivity> DailyActivities => Set<InsightDailyActivity>();
    public DbSet<InsightProcessedEvent> ProcessedEvents => Set<InsightProcessedEvent>();
    public DbSet<InsightEmployeeStatusProjection> EmployeeStatusProjections => Set<InsightEmployeeStatusProjection>();
    public DbSet<InsightEmployeeStatusProcessedEvent> EmployeeStatusProcessedEvents => Set<InsightEmployeeStatusProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InsightDailyActivity>(entity =>
        {
            entity.ToTable("InsightDailyActivity");
            entity.HasKey(x => new { x.EmployeeId, x.ActivityDate });
            entity.Property(x => x.EmployeeId).HasColumnType("char(36)").IsRequired();
            entity.Property(x => x.ActivityDate).HasColumnType("date").IsRequired();
            entity.Property(x => x.HabitActivityCount).HasDefaultValue(0);
            entity.Property(x => x.ChallengeActivityCount).HasDefaultValue(0);
            entity.HasCheckConstraint("CK_InsightDailyActivity_NonNegative",
                "HabitActivityCount >= 0 AND ChallengeActivityCount >= 0");
        });

        modelBuilder.Entity<InsightProcessedEvent>(entity =>
        {
            entity.ToTable("InsightProcessedEvent");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasColumnType("char(36)").IsRequired();
            entity.Property(x => x.ProcessedAtUtc).HasColumnType("datetime(6)").IsRequired();
        });
        modelBuilder.Entity<InsightEmployeeStatusProjection>(entity =>
        {
            entity.ToTable("InsightEmployeeStatusProjection");
            entity.HasKey(x => x.EmployeeId);
            entity.Property(x => x.EmployeeId).HasColumnType("char(36)").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.LifecycleVersion).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime(6)").IsRequired();
        });
        modelBuilder.Entity<InsightEmployeeStatusProcessedEvent>(entity =>
        {
            entity.ToTable("InsightEmployeeStatusProcessedEvent");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasColumnType("char(36)").IsRequired();
            entity.Property(x => x.EmployeeId).HasColumnType("char(36)").IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SchemaVersion).IsRequired();
            entity.Property(x => x.LifecycleVersion).IsRequired();
            entity.Property(x => x.ProcessedAtUtc).HasColumnType("datetime(6)").IsRequired();
        });
    }
}

public sealed class InsightDailyActivity
{
    public Guid EmployeeId { get; set; }
    public DateOnly ActivityDate { get; set; }
    public int HabitActivityCount { get; set; }
    public int ChallengeActivityCount { get; set; }
}

public sealed class InsightProcessedEvent
{
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

public sealed class InsightEmployeeStatusProjection
{
    public Guid EmployeeId { get; set; }
    public bool IsActive { get; set; }
    public long LifecycleVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
public sealed class InsightEmployeeStatusProcessedEvent
{
    public Guid EventId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public long LifecycleVersion { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
