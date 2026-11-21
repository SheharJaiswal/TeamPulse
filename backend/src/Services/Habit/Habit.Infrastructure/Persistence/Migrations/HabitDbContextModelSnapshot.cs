using System;
using Habit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Habit.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HabitDbContext))]
partial class HabitDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("Habit.Domain.Entities.HabitDefinition", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<Guid>("OwnerEmployeeId").HasColumnType("char(36)").HasColumnName("owner_employee_id");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("name");
            b.Property<string>("Description").HasMaxLength(500).HasColumnType("varchar(500)").HasColumnName("description");
            b.Property<string>("Category").HasMaxLength(50).HasColumnType("varchar(50)").HasColumnName("category");
            b.Property<int>("Frequency").HasColumnType("int").HasColumnName("frequency");
            b.Property<int>("TargetCount").HasColumnType("int").HasColumnName("target_count");
            b.Property<DateOnly>("StartDate").HasColumnType("date").HasColumnName("start_date");
            b.Property<DateOnly?>("EndDate").HasColumnType("date").HasColumnName("end_date");
            b.Property<int>("Status").HasColumnType("int").HasColumnName("status");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)").HasColumnName("created_at_utc");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime(6)").HasColumnName("updated_at_utc");
            b.Property<DateTime?>("ArchivedAtUtc").HasColumnType("datetime(6)").HasColumnName("archived_at_utc");
            b.HasKey("Id");
            b.HasIndex("OwnerEmployeeId", "CreatedAtUtc").HasDatabaseName("IX_habits_owner_employee_id_created_at_utc");
            b.HasIndex("OwnerEmployeeId", "Status").HasDatabaseName("IX_habits_owner_employee_id_status");
            b.ToTable("habits");
        });

        modelBuilder.Entity("Habit.Domain.Entities.EmployeeStatusProjection", b =>
        {
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<bool>("IsActive").HasColumnType("tinyint(1)").HasColumnName("is_active");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint").HasColumnName("lifecycle_version");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime(6)").HasColumnName("updated_at_utc");
            b.HasKey("EmployeeId");
            b.ToTable("employee_status_projections");
        });

        modelBuilder.Entity("Habit.Domain.Entities.ProcessedDomainEvent", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)").HasColumnName("event_id");
            b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("event_type");
            b.Property<int>("SchemaVersion").HasColumnType("int").HasColumnName("schema_version");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint").HasColumnName("lifecycle_version");
            b.Property<DateTime>("ProcessedAtUtc").HasColumnType("datetime(6)").HasColumnName("processed_at_utc");
            b.HasKey("EventId");
            b.HasIndex("EmployeeId", "LifecycleVersion").HasDatabaseName("IX_processed_domain_events_employee_version");
            b.ToTable("processed_domain_events");
        });
#pragma warning restore 612, 618
    }
}
