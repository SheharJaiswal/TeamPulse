using System;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Challenge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChallengeDbContext))]
[Migration("20261120200000_AddInboxContentFingerprint")]
public partial class AddInboxContentFingerprint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "fingerprint_algorithm", table: "processed_domain_events", type: "varchar(40)",
            maxLength: 40, nullable: false, defaultValue: "legacy-unfingerprinted");
        migrationBuilder.AddColumn<string>(
            name: "content_fingerprint", table: "processed_domain_events", type: "char(64)",
            nullable: false, defaultValue: new string('0', 64));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "fingerprint_algorithm", table: "processed_domain_events");
        migrationBuilder.DropColumn(name: "content_fingerprint", table: "processed_domain_events");
    }
}
