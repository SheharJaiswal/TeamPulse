using System;

namespace Habit.Domain.Entities;

public sealed class HabitDefinition
{
    private HabitDefinition() { }

    public HabitDefinition(
        Guid id,
        Guid ownerEmployeeId,
        string name,
        string? description,
        string? category,
        HabitFrequency frequency,
        int targetCount,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Habit ID is required.", nameof(id));
        if (ownerEmployeeId == Guid.Empty) throw new ArgumentException("Owner employee ID is required.", nameof(ownerEmployeeId));
        ValidateText(name, 100, nameof(name), required: true);
        ValidateText(description, 500, nameof(description));
        ValidateText(category, 50, nameof(category));
        ValidateFrequency(frequency);
        ValidateTarget(targetCount);
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("End date cannot be earlier than start date.", nameof(endDate));
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Created timestamp must be UTC.", nameof(createdAtUtc));

        Id = id;
        OwnerEmployeeId = ownerEmployeeId;
        Name = name.Trim();
        Description = NormalizeOptional(description);
        Category = NormalizeOptional(category);
        Frequency = frequency;
        TargetCount = targetCount;
        StartDate = startDate;
        EndDate = endDate;
        Status = HabitStatus.Active;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OwnerEmployeeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public HabitFrequency Frequency { get; private set; }
    public int TargetCount { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public HabitStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public void Update(
        string name,
        string? description,
        string? category,
        HabitFrequency frequency,
        int targetCount,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime updatedAtUtc)
    {
        if (Status == HabitStatus.Archived)
            throw new InvalidOperationException("Archived habits cannot be updated.");
        ValidateText(name, 100, nameof(name), required: true);
        ValidateText(description, 500, nameof(description));
        ValidateText(category, 50, nameof(category));
        ValidateFrequency(frequency);
        ValidateTarget(targetCount);
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("End date cannot be earlier than start date.", nameof(endDate));
        if (updatedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Updated timestamp must be UTC.", nameof(updatedAtUtc));

        Name = name.Trim();
        Description = NormalizeOptional(description);
        Category = NormalizeOptional(category);
        Frequency = frequency;
        TargetCount = targetCount;
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Archive(DateTime archivedAtUtc)
    {
        if (archivedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Archive timestamp must be UTC.", nameof(archivedAtUtc));
        if (Status == HabitStatus.Archived) return;
        Status = HabitStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
        UpdatedAtUtc = archivedAtUtc;
    }

    private static void ValidateText(string? value, int maxLength, string parameter, bool required = false)
    {
        if (required && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameter);
        if (value is not null && value.Trim().Length > maxLength)
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameter);
    }

    private static void ValidateFrequency(HabitFrequency frequency)
    {
        if (frequency is not HabitFrequency.Daily and not HabitFrequency.Weekly)
            throw new ArgumentOutOfRangeException(nameof(frequency));
    }

    private static void ValidateTarget(int targetCount)
    {
        if (targetCount is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(targetCount), "Target count must be between 1 and 1000.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
