using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261120030000_InitialIdentitySchema")]
public partial class InitialIdentitySchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "employees",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                NormalizedEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                DisplayName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                PasswordHash = table.Column<string>(type: "longtext", nullable: false),
                OrganizationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                TeamId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                Role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_employees", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_employees_NormalizedEmail",
            table: "employees",
            column: "NormalizedEmail",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "employees");
    }
}
