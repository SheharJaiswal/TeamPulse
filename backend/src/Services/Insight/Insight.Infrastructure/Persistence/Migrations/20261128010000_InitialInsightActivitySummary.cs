using Insight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Insight.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InsightActivityDbContext))]
[Migration("20261128010000_InitialInsightActivitySummary")]
public sealed class InitialInsightActivitySummary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InsightDailyActivity",
            columns: table => new
            {
                EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                ActivityDate = table.Column<DateOnly>(type: "date", nullable: false),
                HabitActivityCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                ChallengeActivityCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsightDailyActivity", x => new { x.EmployeeId, x.ActivityDate });
                table.CheckConstraint("CK_InsightDailyActivity_NonNegative",
                    "HabitActivityCount >= 0 AND ChallengeActivityCount >= 0");
            });

        migrationBuilder.CreateTable(
            name: "InsightProcessedEvent",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_InsightProcessedEvent", x => x.EventId));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InsightProcessedEvent");
        migrationBuilder.DropTable(name: "InsightDailyActivity");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("Insight.Infrastructure.Persistence.InsightDailyActivity", b =>
        {
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)");
            b.Property<DateOnly>("ActivityDate").HasColumnType("date");
            b.Property<int>("ChallengeActivityCount").HasDefaultValue(0).HasColumnType("int");
            b.Property<int>("HabitActivityCount").HasDefaultValue(0).HasColumnType("int");
            b.HasKey("EmployeeId", "ActivityDate");
            b.ToTable("InsightDailyActivity", t =>
                t.HasCheckConstraint("CK_InsightDailyActivity_NonNegative",
                    "HabitActivityCount >= 0 AND ChallengeActivityCount >= 0"));
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
