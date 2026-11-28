using Insight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Insight.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InsightActivityDbContext))]
[Migration("20261128020000_InsightEmployeeLifecycleProjection")]
public sealed class InsightEmployeeLifecycleProjection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("InsightEmployeeStatusProjection",
            columns: table => new
            {
                EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                LifecycleVersion = table.Column<long>(type: "bigint", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_InsightEmployeeStatusProjection", x => x.EmployeeId));

        migrationBuilder.CreateTable("InsightEmployeeStatusProcessedEvent",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "char(36)", nullable: false),
                EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                EventType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                SchemaVersion = table.Column<int>(type: "int", nullable: false),
                LifecycleVersion = table.Column<long>(type: "bigint", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_InsightEmployeeStatusProcessedEvent", x => x.EventId));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("InsightEmployeeStatusProcessedEvent");
        migrationBuilder.DropTable("InsightEmployeeStatusProjection");
    }
}