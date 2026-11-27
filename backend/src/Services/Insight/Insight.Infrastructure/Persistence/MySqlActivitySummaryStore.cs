using Insight.Application.Abstractions;
using Insight.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace Insight.Infrastructure.Persistence;

public sealed class MySqlActivitySummaryStore(
    IDbContextFactory<InsightActivityDbContext> contextFactory) : IActivitySummaryStore
{
    public async Task<ProjectionProcessingOutcome> ProcessAsync(
        ActivityRecordedEnvelope activity,
        ActivityCategory category,
        DateTime processedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            db.ProcessedEvents.Add(new InsightProcessedEvent
            {
                EventId = activity.EventId,
                ProcessedAtUtc = DateTime.SpecifyKind(processedAtUtc, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync(cancellationToken);

            var employeeId = activity.Payload!.EmployeeId.ToString("D");
            var date = activity.Payload.ActivityDate.ToString("yyyy-MM-dd");
            if (category == ActivityCategory.Habit)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO InsightDailyActivity
    (EmployeeId, ActivityDate, HabitActivityCount, ChallengeActivityCount)
VALUES ({employeeId}, {date}, 1, 0)
ON DUPLICATE KEY UPDATE HabitActivityCount = HabitActivityCount + 1;",
                    cancellationToken);
            }
            else
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO InsightDailyActivity
    (EmployeeId, ActivityDate, HabitActivityCount, ChallengeActivityCount)
VALUES ({employeeId}, {date}, 0, 1)
ON DUPLICATE KEY UPDATE ChallengeActivityCount = ChallengeActivityCount + 1;",
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return ProjectionProcessingOutcome.Applied;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectionProcessingOutcome.Duplicate;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ActivitySummaryDay>> GetDailySummaryAsync(
        Guid employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DailyActivities.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.ActivityDate >= fromDate && x.ActivityDate <= toDate)
            .OrderBy(x => x.ActivityDate)
            .Select(x => new ActivitySummaryDay(
                x.ActivityDate, x.HabitActivityCount, x.ChallengeActivityCount))
            .ToListAsync(cancellationToken);
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is MySqlException mysql && mysql.Number == 1062)
                return true;
        return false;
    }
}
