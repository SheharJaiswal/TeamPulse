using Insight.Application.Contracts;

namespace Insight.Application.Abstractions;

public interface IActivitySummaryStore
{
    Task<ProjectionProcessingOutcome> ProcessAsync(
        ActivityRecordedEnvelope activity,
        ActivityCategory category,
        DateTime processedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivitySummaryDay>> GetDailySummaryAsync(
        Guid employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}
