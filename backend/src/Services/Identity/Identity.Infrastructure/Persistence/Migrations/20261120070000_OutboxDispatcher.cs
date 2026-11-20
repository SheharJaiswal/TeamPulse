using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261120070000_OutboxDispatcher")]
public partial class OutboxDispatcher : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "LeaseToken", table: "outbox_messages",
            type: "char(36)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "LeaseExpiresAtUtc", table: "outbox_messages",
            type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "NextAttemptAtUtc", table: "outbox_messages",
            type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "LastAttemptAtUtc", table: "outbox_messages",
            type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "DeadLetteredAtUtc", table: "outbox_messages",
            type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "LastErrorCode", table: "outbox_messages",
            type: "varchar(100)", maxLength: 100, nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_dispatch_queue",
            table: "outbox_messages",
            columns: new[]
            {
                "PublishedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc",
                "LeaseExpiresAtUtc", "CreatedAtUtc"
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_outbox_dispatch_queue", table: "outbox_messages");
        migrationBuilder.DropColumn(name: "LeaseToken", table: "outbox_messages");
        migrationBuilder.DropColumn(name: "LeaseExpiresAtUtc", table: "outbox_messages");
        migrationBuilder.DropColumn(name: "NextAttemptAtUtc", table: "outbox_messages");
        migrationBuilder.DropColumn(name: "LastAttemptAtUtc", table: "outbox_messages");
        migrationBuilder.DropColumn(name: "DeadLetteredAtUtc", table: "outbox_messages");
        migrationBuilder.DropColumn(name: "LastErrorCode", table: "outbox_messages");
    }
}
