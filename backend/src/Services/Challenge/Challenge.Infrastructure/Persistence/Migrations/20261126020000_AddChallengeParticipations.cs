using System;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChallengeDbContext))]
[Migration("20261126020000_AddChallengeParticipations")]
public partial class AddChallengeParticipations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "challenge_participations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                challenge_id = table.Column<Guid>(type: "char(36)", nullable: false),
                employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
                joined_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_challenge_participations", x => x.id));

        migrationBuilder.CreateIndex(
            name: "IX_challenge_participations_challenge_id_employee_id",
            table: "challenge_participations",
            columns: new[] { "challenge_id", "employee_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_challenge_participations_employee_id",
            table: "challenge_participations",
            column: "employee_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "challenge_participations");
    }
}
