using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
partial class IdentityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("Identity.Domain.Entities.Employee", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(150).HasColumnType("varchar(150)");
            b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<long>("LifecycleVersion").ValueGeneratedOnAdd().HasDefaultValue(0L).IsConcurrencyToken().HasColumnType("bigint");
            b.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<string>("OrganizationId").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("longtext");
            b.Property<string>("Role").IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
            b.Property<string>("TeamId").HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<bool>("IsActive").HasColumnType("tinyint(1)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasIndex("NormalizedEmail").IsUnique();
            b.ToTable("employees");
        });

        modelBuilder.Entity("Identity.Domain.Entities.EmployeeInvitation", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)");
            b.Property<DateTime>("AcceptedAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime>("CancelledAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)");
            b.Property<int>("DeliveryGeneration").HasColumnType("int");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(150).HasColumnType("varchar(150)");
            b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<DateTime>("ExpiresAtUtc").HasColumnType("datetime(6)");
            b.Property<Guid>("InvitedByEmployeeId").HasColumnType("char(36)");
            b.Property<string>("NormalizedEmail").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<string>("OrganizationId").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<string>("PendingNormalizedEmail").HasMaxLength(320).HasColumnType("varchar(320)");
            b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("varchar(20)");
            b.Property<string>("TeamId").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<string>("TokenDigest").HasColumnType("char(64)");
            b.HasKey("Id");
            b.HasIndex("PendingNormalizedEmail").IsUnique();
            b.HasIndex("TokenDigest").IsUnique();
            b.HasIndex("Status", "ExpiresAtUtc");
            b.ToTable("employee_invitations");
        });

        modelBuilder.Entity("Identity.Domain.Entities.InvitationDeliverySecret", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)");
            b.Property<int>("Generation").HasColumnType("int");
            b.Property<Guid>("InvitationId").HasColumnType("char(36)");
            b.Property<int>("AttemptCount").HasColumnType("int");
            b.Property<string>("LastErrorCode").HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<Guid?>("LeaseToken").IsConcurrencyToken().HasColumnType("char(36)");
            b.Property<DateTime?>("LeaseExpiresAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime?>("NextAttemptAtUtc").HasColumnType("datetime(6)");
            b.Property<string>("ProtectedToken").IsRequired().HasColumnType("longtext");
            b.HasKey("Id");
            b.HasIndex("InvitationId").IsUnique();
            b.HasIndex("NextAttemptAtUtc", "LeaseExpiresAtUtc", "CreatedAtUtc")
                .HasDatabaseName("IX_invitation_delivery_queue");
            b.ToTable("invitation_delivery_secrets");
        });

        modelBuilder.Entity("Identity.Domain.Entities.InvitationDeliverySecret", b =>
        {
            b.HasOne("Identity.Domain.Entities.EmployeeInvitation", null)
                .WithOne()
                .HasForeignKey("Identity.Domain.Entities.InvitationDeliverySecret", "InvitationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("Identity.Domain.Entities.EmployeeLifecycleAudit", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)");
            b.Property<Guid>("ActorEmployeeId").HasColumnType("char(36)");
            b.Property<Guid>("CorrelationId").HasColumnType("char(36)");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)");
            b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(128).HasColumnType("varchar(128)");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint");
            b.Property<string>("NewStatus").IsRequired().HasMaxLength(20).HasColumnType("varchar(20)");
            b.Property<string>("Note").HasMaxLength(1000).HasColumnType("varchar(1000)");
            b.Property<DateTime>("OccurredAtUtc").HasColumnType("datetime(6)");
            b.Property<string>("PreviousStatus").IsRequired().HasMaxLength(20).HasColumnType("varchar(20)");
            b.Property<string>("ReasonCode").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<string>("RequestFingerprint").IsRequired().HasColumnType("char(64)");
            b.Property<string>("Source").IsRequired().HasMaxLength(50).HasColumnType("varchar(50)");
            b.HasKey("Id");
            b.HasIndex("ActorEmployeeId", "IdempotencyKey").IsUnique();
            b.HasIndex("EmployeeId", "LifecycleVersion").IsUnique();
            b.ToTable("employee_lifecycle_audits");
        });

        modelBuilder.Entity("Identity.Domain.Entities.OutboxMessage", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)");
            b.Property<int>("AttemptCount").HasColumnType("int");
            b.Property<Guid>("CorrelationId").HasColumnType("char(36)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime?>("DeadLetteredAtUtc").HasColumnType("datetime(6)");
            b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<DateTime?>("LastAttemptAtUtc").HasColumnType("datetime(6)");
            b.Property<string>("LastErrorCode").HasMaxLength(100).HasColumnType("varchar(100)");
            b.Property<Guid?>("LeaseToken").IsConcurrencyToken().HasColumnType("char(36)");
            b.Property<DateTime?>("LeaseExpiresAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime?>("NextAttemptAtUtc").HasColumnType("datetime(6)");
            b.Property<DateTime>("OccurredAtUtc").HasColumnType("datetime(6)");
            b.Property<string>("Payload").IsRequired().HasColumnType("longtext");
            b.Property<DateTime?>("PublishedAtUtc").HasColumnType("datetime(6)");
            b.Property<int>("SchemaVersion").HasColumnType("int");
            b.HasKey("EventId");
            b.HasIndex("PublishedAtUtc", "CreatedAtUtc");
            b.HasIndex("PublishedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc", "LeaseExpiresAtUtc", "CreatedAtUtc")
                .HasDatabaseName("IX_outbox_dispatch_queue");
            b.ToTable("outbox_messages");
        });
#pragma warning restore 612, 618
    }
}
