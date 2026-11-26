using System;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Challenge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ChallengeDbContext))]
partial class ChallengeDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11").HasAnnotation("Relational:MaxIdentifierLength", 64);
        modelBuilder.Entity("Challenge.Domain.Entities.Challenge", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<string>("OrganizationId").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("organization_id");
            b.Property<Guid>("CreatedByEmployeeId").HasColumnType("char(36)").HasColumnName("created_by_employee_id");
            b.Property<string>("Name").IsRequired().HasMaxLength(120).HasColumnType("varchar(120)").HasColumnName("name");
            b.Property<string>("Description").HasMaxLength(1000).HasColumnType("varchar(1000)").HasColumnName("description");
            b.Property<DateOnly>("StartDate").HasColumnType("date").HasColumnName("start_date");
            b.Property<DateOnly>("EndDate").HasColumnType("date").HasColumnName("end_date");
            b.Property<DateOnly>("RegistrationDeadline").HasColumnType("date").HasColumnName("registration_deadline");
            b.Property<DateTime?>("RegistrationClosedAtUtc").HasColumnType("datetime(6)").HasColumnName("registration_closed_at_utc");
            b.Property<DateTime?>("TeamsFinalizedAtUtc").HasColumnType("datetime(6)").HasColumnName("teams_finalized_at_utc");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)").HasColumnName("created_at_utc");
            b.HasKey("Id");
            b.HasIndex("OrganizationId", "CreatedAtUtc").HasDatabaseName("IX_challenges_organization_id_created_at_utc");
            b.ToTable("challenges");
        });
        modelBuilder.Entity("Challenge.Domain.Entities.ChallengeParticipation", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<Guid>("ChallengeId").HasColumnType("char(36)").HasColumnName("challenge_id");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<string>("ResponseStatus").IsRequired().HasMaxLength(20).HasColumnType("varchar(20)").HasColumnName("response_status");
            b.Property<DateTime>("RespondedAtUtc").HasColumnType("datetime(6)").HasColumnName("responded_at_utc");
            b.Property<DateTime?>("JoinedAtUtc").HasColumnType("datetime(6)").HasColumnName("joined_at_utc");
            b.HasKey("Id");
            b.HasIndex("ChallengeId", "EmployeeId").IsUnique().HasDatabaseName("IX_challenge_participations_challenge_id_employee_id");
            b.HasIndex("EmployeeId").HasDatabaseName("IX_challenge_participations_employee_id");
            b.ToTable("challenge_participations");
        });
        modelBuilder.Entity("Challenge.Domain.Entities.ChallengeTeam", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<Guid>("ChallengeId").HasColumnType("char(36)").HasColumnName("challenge_id");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("name");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime(6)").HasColumnName("created_at_utc");
            b.HasKey("Id");
            b.HasIndex("ChallengeId", "Name").IsUnique().HasDatabaseName("IX_challenge_teams_challenge_id_name");
            b.ToTable("challenge_teams");
        });
        modelBuilder.Entity("Challenge.Domain.Entities.ChallengeTeamMember", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<Guid>("ChallengeId").HasColumnType("char(36)").HasColumnName("challenge_id");
            b.Property<Guid>("TeamId").HasColumnType("char(36)").HasColumnName("team_id");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<DateTime>("AssignedAtUtc").HasColumnType("datetime(6)").HasColumnName("assigned_at_utc");
            b.HasKey("Id");
            b.HasIndex("ChallengeId", "EmployeeId").IsUnique().HasDatabaseName("IX_challenge_team_members_challenge_id_employee_id");
            b.HasIndex("TeamId").HasDatabaseName("IX_challenge_team_members_team_id");
            b.ToTable("challenge_team_members");
        });
        modelBuilder.Entity("Challenge.Domain.Entities.ChallengeInvitation", b =>
        {
            b.Property<Guid>("Id").HasColumnType("char(36)").HasColumnName("id");
            b.Property<Guid>("ChallengeId").HasColumnType("char(36)").HasColumnName("challenge_id");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<string>("Email").IsRequired().HasMaxLength(320).HasColumnType("varchar(320)").HasColumnName("email");
            b.Property<string>("DisplayName").IsRequired().HasMaxLength(150).HasColumnType("varchar(150)").HasColumnName("display_name");
            b.Property<DateTime>("InvitedAtUtc").HasColumnType("datetime(6)").HasColumnName("invited_at_utc");
            b.Property<DateTime?>("EmailSentAtUtc").HasColumnType("datetime(6)").HasColumnName("email_sent_at_utc");
            b.Property<string>("EmailDeliveryStatus").IsRequired().HasMaxLength(20).HasColumnType("varchar(20)").HasColumnName("email_delivery_status");
            b.HasKey("Id");
            b.HasIndex("ChallengeId", "EmployeeId").IsUnique().HasDatabaseName("IX_challenge_invitations_challenge_id_employee_id");
            b.ToTable("challenge_invitations");
        });
        modelBuilder.Entity("Challenge.Domain.Entities.EmployeeStatusProjection", b =>
        {
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<bool>("IsActive").HasColumnType("tinyint(1)").HasColumnName("is_active");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint").HasColumnName("lifecycle_version");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime(6)").HasColumnName("updated_at_utc");
            b.HasKey("EmployeeId");
            b.ToTable("employee_status_projections");
        });
        modelBuilder.Entity("Challenge.Domain.Entities.ProcessedDomainEvent", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("char(36)").HasColumnName("event_id");
            b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasColumnName("event_type");
            b.Property<int>("SchemaVersion").HasColumnType("int").HasColumnName("schema_version");
            b.Property<Guid>("EmployeeId").HasColumnType("char(36)").HasColumnName("employee_id");
            b.Property<long>("LifecycleVersion").HasColumnType("bigint").HasColumnName("lifecycle_version");
            b.Property<DateTime>("ProcessedAtUtc").HasColumnType("datetime(6)").HasColumnName("processed_at_utc");
            b.Property<string>("FingerprintAlgorithm").IsRequired().HasMaxLength(40).HasColumnType("varchar(40)").HasColumnName("fingerprint_algorithm");
            b.Property<string>("ContentFingerprint").IsRequired().HasColumnType("char(64)").HasColumnName("content_fingerprint");
            b.HasKey("EventId");
            b.HasIndex("EmployeeId", "LifecycleVersion").HasDatabaseName("IX_processed_domain_events_employee_version");
            b.ToTable("processed_domain_events");
        });
#pragma warning restore 612, 618
    }
}