using System;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
#nullable disable
namespace EventTracking.Infrastructure.Persistence.Migrations;
[DbContext(typeof(EventTrackingDbContext))]
partial class EventTrackingDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11").HasAnnotation("Relational:MaxIdentifierLength", 64);
        modelBuilder.Entity("EventTracking.Domain.Entities.EmployeeStatusProjection", b =>
        {
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<bool>("IsActive").HasColumnType("tinyint(1)").HasColumnName("is_active");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint").HasColumnName("lifecycle_version");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime(6)").HasColumnName("updated_at_utc");
            b.HasKey("EmployeeId"); b.ToTable("employee_status_projections");
        });
        modelBuilder.Entity("EventTracking.Domain.Entities.ProcessedDomainEvent", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)").HasColumnName("event_id");
            b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("event_type");
            b.Property<int>("SchemaVersion").HasColumnType("int").HasColumnName("schema_version");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint").HasColumnName("lifecycle_version");
            b.Property<DateTime>("ProcessedAtUtc").HasColumnType("datetime(6)").HasColumnName("processed_at_utc");
            b.HasKey("EventId"); b.ToTable("processed_domain_events");
        });
        modelBuilder.Entity("EventTracking.Domain.Entities.HabitActivitySubmission", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<Guid>("EmployeeId").IsRequired().HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<Guid>("HabitId").IsRequired().HasColumnType("char(36)").HasColumnName("habit_id");
            b.Property<DateOnly>("ActivityDate").HasColumnType("date").HasColumnName("activity_date");
            b.Property<int>("Quantity").HasColumnType("int").HasColumnName("quantity");
            b.Property<Guid>("IdempotencyKey").IsRequired().HasColumnType("char(36)").HasColumnName("idempotency_key");
            b.Property<DateTime>("SubmittedAtUtc").HasColumnType("datetime(6)").HasColumnName("submitted_at_utc");
            b.HasKey("Id");
            b.HasIndex("EmployeeId", "HabitId", "ActivityDate").HasDatabaseName("ix_habit_activity_employee_habit_date");
            b.HasIndex("EmployeeId", "IdempotencyKey").IsUnique().HasDatabaseName("ux_habit_activity_employee_idempotency");
            b.ToTable("habit_activity_submissions");
        });
        modelBuilder.Entity("EventTracking.Domain.Entities.EventTrackingOutboxMessage", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)").HasColumnName("EventId");
            b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<int>("SchemaVersion").HasColumnType("int");
            b.Property<DateTime>("OccurredAtUtc").HasColumnType("datetime(6)");
            b.Property<Guid>("CorrelationId").HasColumnType("char(36)");
            b.Property<string>("Payload").IsRequired().HasColumnType("longtext");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime?>("PublishedAtUtc").HasColumnType("datetime(6)");
            b.Property<int>("AttemptCount").HasColumnType("int");
            b.Property<Guid?>("LeaseToken").HasColumnType("char(36)");
            b.Property<DateTime?>("LeaseExpiresAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime?>("NextAttemptAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime?>("DeadLetteredAtUtc").HasColumnType("datetime(6)");
            b.Property<string>("LastErrorCode").HasMaxLength(100).HasColumnType("varchar(100)");
            b.HasKey("EventId");
            b.HasIndex("PublishedAtUtc", "CreatedAtUtc");
            b.HasIndex("PublishedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc", "LeaseExpiresAtUtc", "CreatedAtUtc")
                .HasDatabaseName("IX_event_tracking_outbox_dispatch_queue");
            b.ToTable("event_tracking_outbox_messages");
        });
#pragma warning restore 612, 618
    }
}
