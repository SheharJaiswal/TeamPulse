using System;
using Habit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habit.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HabitDbContext))]
[Migration("20261121150000_AddHabitDefinitions")]
public partial class AddHabitDefinitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "habits",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                owner_employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                frequency = table.Column<int>(type: "int", nullable: false),
                target_count = table.Column<int>(type: "int", nullable: false),
                start_date = table.Column<DateOnly>(type: "date", nullable: false),
                end_date = table.Column<DateOnly>(type: "date", nullable: true),
                status = table.Column<int>(type: "int", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                archived_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_habits", x => x.id));

        migrationBuilder.CreateIndex(
            name: "IX_habits_owner_employee_id_created_at_utc",
            table: "habits",
            columns: new[] { "owner_employee_id", "created_at_utc" });
        migrationBuilder.CreateIndex(
            name: "IX_habits_owner_employee_id_status",
            table: "habits",
            columns: new[] { "owner_employee_id", "status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "habits");
}
