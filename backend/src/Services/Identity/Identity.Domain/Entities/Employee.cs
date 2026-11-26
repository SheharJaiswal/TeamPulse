using System;

namespace Identity.Domain.Entities;

public sealed class Employee
{
    private Employee() { }

    public Employee(Guid id, string email, string normalizedEmail, string displayName,
        string passwordHash, string organizationId, string? teamId, string role)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        OrganizationId = organizationId;
        TeamId = string.IsNullOrWhiteSpace(teamId) ? null : teamId.Trim();
        Role = role;
        IsActive = true;
        LifecycleVersion = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string OrganizationId { get; private set; } = string.Empty;
    public string? TeamId { get; private set; }
    public string Role { get; private set; } = "Employee";
    public bool IsActive { get; private set; }
    public long LifecycleVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public bool InitializeActiveLifecycleVersion()
    {
        if (!IsActive || LifecycleVersion != 0)
            return false;

        LifecycleVersion = 1;
        return true;
    }

    public bool ChangeActiveState(bool isActive)
    {
        if (IsActive == isActive)
            return false;

        IsActive = isActive;
        LifecycleVersion = checked(LifecycleVersion + 1);
        return true;
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        PasswordHash = passwordHash;
    }
}
