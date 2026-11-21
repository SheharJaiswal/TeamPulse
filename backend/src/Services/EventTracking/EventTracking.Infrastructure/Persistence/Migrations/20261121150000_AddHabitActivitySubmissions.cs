using System;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace EventTracking.Infrastructure.Persistence.Migrations;
[DbContext(typeof(EventTrackingDbContext))]
[Migration("20261121150000_AddHabitActivitySubmissions")]
public partial class AddHabitActivitySubmissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "habit_activity_submissions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                habit_id = table.Column<Guid>(type: "char(36)", nullable: false),
                activity_date = table.Column<DateOnly>(type: "date", nullable: false),
                quantity = table.Column<int>(type: "int", nullable: false),
                idempotency_key = table.Column<Guid>(type: "char(36)", nullable: false),
                submitted_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_habit_activity_submissions", x => x.id));
        migrationBuilder.CreateIndex(name: "ux_habit_activity_employee_idempotency",
            table: "habit_activity_submissions", columns: new[] { "employee_id", "idempotency_key" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_habit_activity_employee_habit_date",
            table: "habit_activity_submissions", columns: new[] { "employee_id", "habit_id", "activity_date" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "habit_activity_submissions");
}
