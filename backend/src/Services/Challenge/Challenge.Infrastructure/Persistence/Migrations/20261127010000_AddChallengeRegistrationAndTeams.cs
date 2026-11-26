using System;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChallengeDbContext))]
[Migration("20261127010000_AddChallengeRegistrationAndTeams")]
public partial class AddChallengeRegistrationAndTeams : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(name: "registration_deadline", table: "challenges", type: "date", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "registration_closed_at_utc", table: "challenges", type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "teams_finalized_at_utc", table: "challenges", type: "datetime(6)", nullable: true);
        migrationBuilder.Sql("UPDATE challenges SET registration_deadline = start_date, registration_closed_at_utc = created_at_utc");
        migrationBuilder.AlterColumn<DateOnly>(name: "registration_deadline", table: "challenges", type: "date", nullable: false,
            oldClrType: typeof(DateOnly), oldType: "date", oldNullable: true);

        migrationBuilder.AddColumn<string>(name: "response_status", table: "challenge_participations", type: "varchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "responded_at_utc", table: "challenge_participations", type: "datetime(6)", nullable: true);
        migrationBuilder.Sql("UPDATE challenge_participations SET response_status = 'Joined', responded_at_utc = joined_at_utc");
        migrationBuilder.AlterColumn<string>(name: "response_status", table: "challenge_participations", type: "varchar(20)", maxLength: 20, nullable: false,
            oldClrType: typeof(string), oldType: "varchar(20)", oldMaxLength: 20, oldNullable: true);
        migrationBuilder.AlterColumn<DateTime>(name: "responded_at_utc", table: "challenge_participations", type: "datetime(6)", nullable: false,
            oldClrType: typeof(DateTime), oldType: "datetime(6)", oldNullable: true);
        migrationBuilder.AlterColumn<DateTime>(name: "joined_at_utc", table: "challenge_participations", type: "datetime(6)", nullable: true,
            oldClrType: typeof(DateTime), oldType: "datetime(6)");

        migrationBuilder.CreateTable(name: "challenge_invitations", columns: table => new
        {
            id = table.Column<Guid>(type: "char(36)", nullable: false),
            challenge_id = table.Column<Guid>(type: "char(36)", nullable: false),
            employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
            email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
            display_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
            invited_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            email_sent_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            email_delivery_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
        }, constraints: table => table.PrimaryKey("PK_challenge_invitations", x => x.id));

        migrationBuilder.CreateTable(name: "challenge_teams", columns: table => new
        {
            id = table.Column<Guid>(type: "char(36)", nullable: false),
            challenge_id = table.Column<Guid>(type: "char(36)", nullable: false),
            name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
            created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_challenge_teams", x => x.id));

        migrationBuilder.CreateTable(name: "challenge_team_members", columns: table => new
        {
            id = table.Column<Guid>(type: "char(36)", nullable: false),
            challenge_id = table.Column<Guid>(type: "char(36)", nullable: false),
            team_id = table.Column<Guid>(type: "char(36)", nullable: false),
            employee_id = table.Column<Guid>(type: "char(36)", nullable: false),
            assigned_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_challenge_team_members", x => x.id));

        migrationBuilder.CreateIndex(name: "IX_challenge_invitations_challenge_id_employee_id", table: "challenge_invitations", columns: new[] { "challenge_id", "employee_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_challenge_teams_challenge_id_name", table: "challenge_teams", columns: new[] { "challenge_id", "name" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_challenge_team_members_challenge_id_employee_id", table: "challenge_team_members", columns: new[] { "challenge_id", "employee_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_challenge_team_members_team_id", table: "challenge_team_members", column: "team_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "challenge_invitations");
        migrationBuilder.DropTable(name: "challenge_team_members");
        migrationBuilder.DropTable(name: "challenge_teams");
        migrationBuilder.DropColumn(name: "registration_deadline", table: "challenges");
        migrationBuilder.DropColumn(name: "registration_closed_at_utc", table: "challenges");
        migrationBuilder.DropColumn(name: "teams_finalized_at_utc", table: "challenges");
        migrationBuilder.Sql("UPDATE challenge_participations SET joined_at_utc = responded_at_utc WHERE joined_at_utc IS NULL");
        migrationBuilder.DropColumn(name: "response_status", table: "challenge_participations");
        migrationBuilder.DropColumn(name: "responded_at_utc", table: "challenge_participations");
        migrationBuilder.AlterColumn<DateTime>(name: "joined_at_utc", table: "challenge_participations", type: "datetime(6)", nullable: false,
            oldClrType: typeof(DateTime), oldType: "datetime(6)", oldNullable: true);
    }
}