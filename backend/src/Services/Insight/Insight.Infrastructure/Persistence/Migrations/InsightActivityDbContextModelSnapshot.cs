using System;
using Insight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
#nullable disable
namespace Insight.Infrastructure.Persistence.Migrations;
[DbContext(typeof(InsightActivityDbContext))]
partial class InsightActivityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11").HasAnnotation("Relational:MaxIdentifierLength", 64);
        modelBuilder.Entity("Insight.Infrastructure.Persistence.InsightDailyActivity", b =>
        {
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)");
            b.Property<DateOnly>("ActivityDate").HasColumnType("date");
            b.Property<int>("ChallengeActivityCount").HasDefaultValue(0).HasColumnType("int");
            b.Property<int>("HabitActivityCount").HasDefaultValue(0).HasColumnType("int");
            b.HasKey("EmployeeId", "ActivityDate");
            b.ToTable("InsightDailyActivity", t => t.HasCheckConstraint("CK_InsightDailyActivity_NonNegative", "HabitActivityCount >= 0 AND ChallengeActivityCount >= 0"));
        });
        modelBuilder.Entity("Insight.Infrastructure.Persistence.InsightEmployeeStatusProjection", b =>
        {
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)");
            b.Property<bool>("IsActive").HasColumnType("tinyint(1)");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime(6)");
            b.HasKey("EmployeeId");
            b.ToTable("InsightEmployeeStatusProjection");
        });
        modelBuilder.Entity("Insight.Infrastructure.Persistence.InsightEmployeeStatusProcessedEvent", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)");
            b.Property<string>("EventType").HasMaxLength(64).HasColumnType("varchar(64)");
            b.Property<int>("SchemaVersion").HasColumnType("int");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint");
            b.Property<DateTime>("ProcessedAtUtc").HasColumnType("datetime(6)");
            b.HasKey("EventId");
            b.ToTable("InsightEmployeeStatusProcessedEvent");
        });
        modelBuilder.Entity("Insight.Infrastructure.Persistence.InsightProcessedEvent", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)");
            b.Property<DateTime>("ProcessedAtUtc").HasColumnType("datetime(6)");
            b.HasKey("EventId");
            b.ToTable("InsightProcessedEvent");
        });
#pragma warning restore 612, 618
    }
}