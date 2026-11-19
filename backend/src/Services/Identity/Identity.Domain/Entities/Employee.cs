using System;

namespace Identity.Domain.Entities;

public sealed class Employee
{
    private Employee() { }
    public Employee(Guid id, string email, string normalizedEmail, string displayName,
        string passwordHash, string organizationId, string teamId, string role)
    {
        Id=id; Email=email; NormalizedEmail=normalizedEmail; DisplayName=displayName;
        PasswordHash=passwordHash; OrganizationId=organizationId; TeamId=teamId;
        Role=role; IsActive=true; CreatedAtUtc=DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string OrganizationId { get; private set; } = string.Empty;
    public string TeamId { get; private set; } = string.Empty;
    public string Role { get; private set; } = "Employee";
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}