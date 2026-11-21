using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261121080000_EmployeeInvitations")]
public partial class EmployeeInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "employee_invitations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                NormalizedEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                PendingNormalizedEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true),
                DisplayName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                OrganizationId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                TeamId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                TokenDigest = table.Column<string>(type: "char(64)", nullable: false),
                InvitedByEmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                AcceptedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CancelledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                DeliveryGeneration = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_employee_invitations", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_employee_invitations_PendingNormalizedEmail",
            table: "employee_invitations",
            column: "PendingNormalizedEmail",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_employee_invitations_TokenDigest",
            table: "employee_invitations",
            column: "TokenDigest",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_employee_invitations_Status_ExpiresAtUtc",
            table: "employee_invitations",
            columns: new[] { "Status", "ExpiresAtUtc" });

        migrationBuilder.CreateTable(
            name: "invitation_delivery_secrets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                InvitationId = table.Column<Guid>(type: "char(36)", nullable: false),
                Generation = table.Column<int>(type: "int", nullable: false),
                ProtectedToken = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_invitation_delivery_secrets", x => x.Id);
                table.ForeignKey(
                    name: "FK_invitation_delivery_secrets_employee_invitations_InvitationId",
                    column: x => x.InvitationId,
                    principalTable: "employee_invitations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_invitation_delivery_secrets_InvitationId",
            table: "invitation_delivery_secrets",
            column: "InvitationId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "invitation_delivery_secrets");
        migrationBuilder.DropTable(name: "employee_invitations");
    }
}
