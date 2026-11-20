namespace Habit.Application.Contracts;

public enum ProjectionProcessingOutcome
{
    Applied,
    Stale,
    Duplicate,
    Invalid
}
