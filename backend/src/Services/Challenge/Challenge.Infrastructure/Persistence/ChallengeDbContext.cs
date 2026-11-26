using Challenge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Challenge.Infrastructure.Persistence;

public sealed class ChallengeDbContext(DbContextOptions<ChallengeDbContext> options) : DbContext(options)
{
    public DbSet<EmployeeStatusProjection> EmployeeStatusProjections => Set<EmployeeStatusProjection>();
    public DbSet<ProcessedDomainEvent> ProcessedDomainEvents => Set<ProcessedDomainEvent>();
    public DbSet<Challenge.Domain.Entities.Challenge> Challenges => Set<Challenge.Domain.Entities.Challenge>();
    public DbSet<ChallengeParticipation> ChallengeParticipations => Set<ChallengeParticipation>();
    public DbSet<ChallengeTeam> ChallengeTeams => Set<ChallengeTeam>();
    public DbSet<ChallengeTeamMember> ChallengeTeamMembers => Set<ChallengeTeamMember>();
    public DbSet<ChallengeInvitation> ChallengeInvitations => Set<ChallengeInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var challenge = modelBuilder.Entity<Challenge.Domain.Entities.Challenge>();
        challenge.ToTable("challenges");
        challenge.HasKey(x => x.Id);
        challenge.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        challenge.Property(x => x.OrganizationId).HasColumnName("organization_id").HasMaxLength(100).IsRequired();
        challenge.Property(x => x.CreatedByEmployeeId).HasColumnName("created_by_employee_id").HasColumnType("char(36)").IsRequired();
        challenge.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        challenge.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        challenge.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        challenge.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        challenge.Property(x => x.RegistrationDeadline).HasColumnName("registration_deadline").HasColumnType("date").IsRequired();
        challenge.Property(x => x.RegistrationClosedAtUtc).HasColumnName("registration_closed_at_utc").HasColumnType("datetime(6)");
        challenge.Property(x => x.TeamsFinalizedAtUtc).HasColumnName("teams_finalized_at_utc").HasColumnType("datetime(6)");
        challenge.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
        challenge.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc }).HasDatabaseName("IX_challenges_organization_id_created_at_utc");

        var participation = modelBuilder.Entity<ChallengeParticipation>();
        participation.ToTable("challenge_participations");
        participation.HasKey(x => x.Id);
        participation.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        participation.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasColumnType("char(36)").IsRequired();
        participation.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        participation.Property(x => x.ResponseStatus).HasColumnName("response_status").HasMaxLength(20).IsRequired();
        participation.Property(x => x.RespondedAtUtc).HasColumnName("responded_at_utc").HasColumnType("datetime(6)").IsRequired();
        participation.Property(x => x.JoinedAtUtc).HasColumnName("joined_at_utc").HasColumnType("datetime(6)");
        participation.HasIndex(x => new { x.ChallengeId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("IX_challenge_participations_challenge_id_employee_id");
        participation.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_challenge_participations_employee_id");

        var team = modelBuilder.Entity<ChallengeTeam>();
        team.ToTable("challenge_teams");
        team.HasKey(x => x.Id);
        team.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        team.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasColumnType("char(36)").IsRequired();
        team.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        team.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
        team.HasIndex(x => new { x.ChallengeId, x.Name }).IsUnique().HasDatabaseName("IX_challenge_teams_challenge_id_name");

        var member = modelBuilder.Entity<ChallengeTeamMember>();
        member.ToTable("challenge_team_members");
        member.HasKey(x => x.Id);
        member.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        member.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasColumnType("char(36)").IsRequired();
        member.Property(x => x.TeamId).HasColumnName("team_id").HasColumnType("char(36)").IsRequired();
        member.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        member.Property(x => x.AssignedAtUtc).HasColumnName("assigned_at_utc").HasColumnType("datetime(6)").IsRequired();
        member.HasIndex(x => new { x.ChallengeId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("IX_challenge_team_members_challenge_id_employee_id");
        member.HasIndex(x => x.TeamId).HasDatabaseName("IX_challenge_team_members_team_id");

        var invitation = modelBuilder.Entity<ChallengeInvitation>();
        invitation.ToTable("challenge_invitations");
        invitation.HasKey(x => x.Id);
        invitation.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        invitation.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasColumnType("char(36)").IsRequired();
        invitation.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        invitation.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        invitation.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(150).IsRequired();
        invitation.Property(x => x.InvitedAtUtc).HasColumnName("invited_at_utc").HasColumnType("datetime(6)").IsRequired();
        invitation.Property(x => x.EmailSentAtUtc).HasColumnName("email_sent_at_utc").HasColumnType("datetime(6)");
        invitation.Property(x => x.EmailDeliveryStatus).HasColumnName("email_delivery_status").HasMaxLength(20).IsRequired();
        invitation.HasIndex(x => new { x.ChallengeId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("IX_challenge_invitations_challenge_id_employee_id");

        var projection = modelBuilder.Entity<EmployeeStatusProjection>();
        projection.ToTable("employee_status_projections");
        projection.HasKey(x => x.EmployeeId);
        projection.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)");
        projection.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("tinyint(1)").IsRequired();
        projection.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint").IsRequired();
        projection.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();

        var inbox = modelBuilder.Entity<ProcessedDomainEvent>();
        inbox.ToTable("processed_domain_events");
        inbox.HasKey(x => x.EventId);
        inbox.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)");
        inbox.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        inbox.Property(x => x.SchemaVersion).HasColumnName("schema_version").IsRequired();
        inbox.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)");
        inbox.Property(x => x.LifecycleVersion).HasColumnName("lifecycle_version").HasColumnType("bigint");
        inbox.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("datetime(6)");
        inbox.Property(x => x.FingerprintAlgorithm).HasColumnName("fingerprint_algorithm").HasMaxLength(40).IsRequired();
        inbox.Property(x => x.ContentFingerprint).HasColumnName("content_fingerprint").HasColumnType("char(64)").IsRequired();
        inbox.HasIndex(x => new { x.EmployeeId, x.LifecycleVersion }).HasDatabaseName("IX_processed_domain_events_employee_version");
    }
}