using Challenge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Challenge.Infrastructure.Persistence;

public sealed class ChallengeDbContext(DbContextOptions<ChallengeDbContext> options) : DbContext(options)
{
    public DbSet<EmployeeStatusProjection> EmployeeStatusProjections => Set<EmployeeStatusProjection>();
    public DbSet<ProcessedDomainEvent> ProcessedDomainEvents => Set<ProcessedDomainEvent>();
    public DbSet<Challenge.Domain.Entities.Challenge> Challenges => Set<Challenge.Domain.Entities.Challenge>();
    public DbSet<ChallengeParticipation> ChallengeParticipations => Set<ChallengeParticipation>();

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
        challenge.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
        challenge.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc }).HasDatabaseName("IX_challenges_organization_id_created_at_utc");

        var participation = modelBuilder.Entity<ChallengeParticipation>();
        participation.ToTable("challenge_participations");
        participation.HasKey(x => x.Id);
        participation.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)");
        participation.Property(x => x.ChallengeId).HasColumnName("challenge_id").HasColumnType("char(36)").IsRequired();
        participation.Property(x => x.EmployeeId).HasColumnName("employee_id").HasColumnType("char(36)").IsRequired();
        participation.Property(x => x.JoinedAtUtc).HasColumnName("joined_at_utc").HasColumnType("datetime(6)").IsRequired();
        participation.HasIndex(x => new { x.ChallengeId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("IX_challenge_participations_challenge_id_employee_id");
        participation.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_challenge_participations_employee_id");

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
