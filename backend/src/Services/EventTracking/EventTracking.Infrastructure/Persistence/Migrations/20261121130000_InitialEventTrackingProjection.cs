using System;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace EventTracking.Infrastructure.Persistence.Migrations;
[DbContext(typeof(EventTrackingDbContext))]
[Migration("20261121130000_InitialEventTrackingProjection")]
public partial class InitialEventTrackingProjection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "employee_status_projections",
            columns: table => new
            {
                employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                lifecycle_version = table.Column<long>(type: "bigint", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_employee_status_projections", x => x.employee_id));
        migrationBuilder.CreateTable(name: "processed_domain_events",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "char(36)", nullable: false),
                event_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                schema_version = table.Column<int>(type: "int", nullable: false),
                employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                lifecycle_version = table.Column<long>(type: "bigint", nullable: false),
                processed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_processed_domain_events", x => x.event_id));
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "processed_domain_events");
        migrationBuilder.DropTable(name: "employee_status_projections");
    }
}