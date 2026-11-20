using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261120060000_EmployeeLifecycleOutbox")]
public partial class EmployeeLifecycleOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "LifecycleVersion",
            table: "employees",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "employee_lifecycle_audits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                ActorEmployeeId = table.Column<Guid>(type: "char(36)", nullable: false),
                Source = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                PreviousStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                NewStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                LifecycleVersion = table.Column<long>(type: "bigint", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ReasonCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                CorrelationId = table.Column<Guid>(type: "char(36)", nullable: false),
                IdempotencyKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                RequestFingerprint = table.Column<string>(type: "char(64)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_employee_lifecycle_audits", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_employee_lifecycle_audits_ActorEmployeeId_IdempotencyKey",
            table: "employee_lifecycle_audits",
            columns: new[] { "ActorEmployeeId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_employee_lifecycle_audits_EmployeeId_LifecycleVersion",
            table: "employee_lifecycle_audits",
            columns: new[] { "EmployeeId", "LifecycleVersion" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "outbox_messages",
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
                AttemptCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_outbox_messages", x => x.EventId));

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_PublishedAtUtc_CreatedAtUtc",
            table: "outbox_messages",
            columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "employee_lifecycle_audits");
        migrationBuilder.DropTable(name: "outbox_messages");
        migrationBuilder.DropColumn(name: "LifecycleVersion", table: "employees");
    }
}
