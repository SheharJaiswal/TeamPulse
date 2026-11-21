namespace Identity.Application.Contracts;

public sealed record ActivateEmployeeInvitationRequest(string Token, string Password);

public enum EmployeeInvitationActivationStatus
{
    Activated,
    Invalid
}

public sealed record EmployeeInvitationActivationResult(
    EmployeeInvitationActivationStatus Status);
