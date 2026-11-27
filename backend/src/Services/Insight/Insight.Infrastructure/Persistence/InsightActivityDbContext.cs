using Insight.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Insight.Infrastructure.Persistence;

public sealed class InsightActivityDbContext(DbContextOptions<InsightActivityDbContext> options)
    : DbContext(options)
{
    public DbSet<InsightDailyActivity> DailyActivities => Set<InsightDailyActivity>();
    public DbSet<InsightProcessedEvent> ProcessedEvents => Set<InsightProcessedEvent>();

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
