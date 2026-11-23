using System;

namespace Identity.Application.Contracts;

public sealed record CreateEmployeeInvitationRequest(
    string Email,
    string DisplayName,
    string OrganizationId,
    string? TeamId = null);

public sealed record EmployeeInvitationSummary(
    Guid InvitationId,
    string Email,
    string DisplayName,
    string OrganizationId,
    string? TeamId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    int DeliveryGeneration);

public enum EmployeeInvitationCommandStatus
{
    Created,
    Updated,
    NotFound,
    Conflict,
    Expired,
    AlreadyTerminal
}

public sealed record EmployeeInvitationCommandResult(
    EmployeeInvitationCommandStatus Status,
    EmployeeInvitationSummary? Invitation);
