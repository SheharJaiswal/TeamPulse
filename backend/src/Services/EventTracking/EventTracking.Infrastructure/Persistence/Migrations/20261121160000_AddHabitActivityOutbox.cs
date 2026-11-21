using System;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace EventTracking.Infrastructure.Persistence.Migrations;
[DbContext(typeof(EventTrackingDbContext))]
[Migration("20261121160000_AddHabitActivityOutbox")]
public partial class AddHabitActivityOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "event_tracking_outbox_messages",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "char(36)", nullable: false),
                EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                SchemaVersion = table.Column<int>(type: "int", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CorrelationId = table.Column<Guid>(type: "char(36)", nullable: false),
                Payload = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                LeaseToken = table.Column<Guid>(type: "char(36)", nullable: true),
                LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                DeadLetteredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
            }, constraints: table => table.PrimaryKey("PK_event_tracking_outbox_messages", x => x.EventId));
        migrationBuilder.CreateIndex(name: "IX_event_tracking_outbox_messages_PublishedAtUtc_CreatedAtUtc",
            table: "event_tracking_outbox_messages", columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_event_tracking_outbox_dispatch_queue",
            table: "event_tracking_outbox_messages",
            columns: new[] { "PublishedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc", "LeaseExpiresAtUtc", "CreatedAtUtc" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("event_tracking_outbox_messages");
}
