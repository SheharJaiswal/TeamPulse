using System;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChallengeDbContext))]
[Migration("20261127020000_AddChallengeGoals")]
public partial class AddChallengeGoals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "goal_instructions", table: "challenges",
            type: "varchar(500)", maxLength: 500, nullable: false,
            defaultValue: "Complete the challenge activity and record your progress.");
        migrationBuilder.AddColumn<decimal>(name: "goal_target", table: "challenges",
            type: "decimal(12,2)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<string>(name: "goal_unit", table: "challenges",
            type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "completion");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "goal_instructions", table: "challenges");
        migrationBuilder.DropColumn(name: "goal_target", table: "challenges");
        migrationBuilder.DropColumn(name: "goal_unit", table: "challenges");
    }
}
