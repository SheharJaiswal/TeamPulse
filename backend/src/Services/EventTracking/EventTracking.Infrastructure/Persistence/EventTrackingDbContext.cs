using EventTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace EventTracking.Infrastructure.Persistence;
public sealed class EventTrackingDbContext(DbContextOptions<EventTrackingDbContext> options) : DbContext(options)
{
    public DbSet<EmployeeStatusProjection> EmployeeStatusProjections => Set<EmployeeStatusProjection>();
    public DbSet<ProcessedDomainEvent> ProcessedDomainEvents => Set<ProcessedDomainEvent>();
    public DbSet<HabitActivitySubmission> HabitActivitySubmissions => Set<HabitActivitySubmission>();
    public DbSet<ChallengeActivitySubmission> ChallengeActivitySubmissions => Set<ChallengeActivitySubmission>();
    public DbSet<EventTrackingOutboxMessage> EventTrackingOutboxMessages => Set<EventTrackingOutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var projection = modelBuilder.Entity<EmployeeStatusProjection>();
        projection.ToTable("employee_status_projections"); projection.HasKey(x => x.EmployeeId);
        projection.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)");
        projection.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("tinyint(1)").IsRequired();
        projection.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint").IsRequired();
        projection.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();
        var inbox = modelBuilder.Entity<ProcessedDomainEvent>();
        inbox.ToTable("processed_domain_events"); inbox.HasKey(x => x.EventId);
        inbox.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)");
        inbox.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        inbox.Property(x => x.SchemaVersion).HasColumnName("schema_version").IsRequired();
        inbox.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        inbox.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint").IsRequired();
        inbox.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("datetime(6)").IsRequired();
        var activity = modelBuilder.Entity<HabitActivitySubmission>();
        activity.ToTable("habit_activity_submissions"); activity.HasKey(x => x.Id);
        activity.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        activity.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        activity.Property(x => x.HabitId).HasColumnName("habit_id").HasColumnType("char(36)").IsRequired();
        activity.Property(x => x.ActivityDate).HasColumnName("activity_date").HasColumnType("date").IsRequired();
        activity.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        activity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("char(36)").IsRequired();
        activity.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("datetime(6)").IsRequired();
        activity.HasIndex(x => new { x.EmployeeId, x.IdempotencyKey }).IsUnique().HasDatabaseName("ux_habit_activity_employee_idempotency");
        activity.HasIndex(x => new { x.EmployeeId, x.HabitId, x.ActivityDate }).HasDatabaseName("ix_habit_activity_employee_habit_date");
        var challengeActivity = modelBuilder.Entity<ChallengeActivitySubmission>();
        challengeActivity.ToTable("challenge_activity_submissions"); challengeActivity.HasKey(x => x.Id);
        challengeActivity.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        challengeActivity.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        challengeActivity.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasColumnType("char(36)").IsRequired();
        challengeActivity.Property(x => x.ActivityDate).HasColumnName("activity_date").HasColumnType("date").IsRequired();
        challengeActivity.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("decimal(12,2)").IsRequired();
        challengeActivity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("char(36)").IsRequired();
        challengeActivity.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("datetime(6)").IsRequired();
        challengeActivity.HasIndex(x => new { x.EmployeeId, x.IdempotencyKey }).IsUnique().HasDatabaseName("ux_challenge_activity_employee_idempotency");
        challengeActivity.HasIndex(x => new { x.EmployeeId, x.ChallengeId, x.ActivityDate }).HasDatabaseName("ix_challenge_activity_employee_challenge_date");
        var outbox = modelBuilder.Entity<EventTrackingOutboxMessage>();
        outbox.ToTable("event_tracking_outbox_messages"); outbox.HasKey(x => x.EventId);
        outbox.Property(x => x.EventId).HasColumnName("EventId").HasColumnType("char(36)");
        outbox.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        outbox.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
        outbox.Property(x => x.LeaseToken).IsConcurrencyToken();
        outbox.Property(x => x.LastErrorCode).HasMaxLength(100);
        outbox.HasIndex(x => new { x.PublishedAtUtc, x.CreatedAtUtc });
        outbox.HasIndex(x => new { x.PublishedAtUtc, x.DeadLetteredAtUtc, x.NextAttemptAtUtc, x.LeaseExpiresAtUtc, x.CreatedAtUtc })
            .HasDatabaseName("IX_event_tracking_outbox_dispatch_queue");
    }
}
