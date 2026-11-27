using System;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace EventTracking.Infrastructure.Persistence.Migrations;
[DbContext(typeof(EventTrackingDbContext))]
[Migration("20261127020000_AddChallengeActivitySubmissions")]
public partial class AddChallengeActivitySubmissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "challenge_activity_submissions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                challenge_id = table.Column<Guid>(type: "char(36)", nullable: false),
                activity_date = table.Column<DateOnly>(type: "date", nullable: false),
                quantity = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                idempotency_key = table.Column<Guid>(type: "char(36)", nullable: false),
                submitted_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_challenge_activity_submissions", x => x.id));
        migrationBuilder.CreateIndex(name: "ux_challenge_activity_employee_idempotency",
            table: "challenge_activity_submissions", columns: new[] { "employee_id", "idempotency_key" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_challenge_activity_employee_challenge_date",
            table: "challenge_activity_submissions", columns: new[] { "employee_id", "challenge_id", "activity_date" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "challenge_activity_submissions");
}
