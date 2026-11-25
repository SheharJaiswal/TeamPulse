using System;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChallengeDbContext))]
[Migration("20261126010000_AddChallenges")]
public partial class AddChallenges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "challenges",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                organization_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                created_by_employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                start_date = table.Column<DateOnly>(type: "date", nullable: false),
                end_date = table.Column<DateOnly>(type: "date", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_challenges", x => x.id));

        migrationBuilder.CreateIndex(name: "IX_challenges_organization_id_created_at_utc",
            table: "challenges", columns: new[] { "organization_id", "created_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "challenges");
    }
}
