using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261121160000_InvitationDeliveryLeases")]
public partial class InvitationDeliveryLeases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AttemptCount",
            table: "invitation_delivery_secrets",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "LastErrorCode",
            table: "invitation_delivery_secrets",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LeaseToken",
            table: "invitation_delivery_secrets",
            type: "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LeaseExpiresAtUtc",
            table: "invitation_delivery_secrets",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextAttemptAtUtc",
            table: "invitation_delivery_secrets",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_invitation_delivery_queue",
            table: "invitation_delivery_secrets",
            columns: new[] { "NextAttemptAtUtc", "LeaseExpiresAtUtc", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_invitation_delivery_queue",
            table: "invitation_delivery_secrets");

        migrationBuilder.DropColumn(name: "AttemptCount", table: "invitation_delivery_secrets");
        migrationBuilder.DropColumn(name: "LastErrorCode", table: "invitation_delivery_secrets");
        migrationBuilder.DropColumn(name: "LeaseToken", table: "invitation_delivery_secrets");
        migrationBuilder.DropColumn(name: "LeaseExpiresAtUtc", table: "invitation_delivery_secrets");
        migrationBuilder.DropColumn(name: "NextAttemptAtUtc", table: "invitation_delivery_secrets");
    }
}
