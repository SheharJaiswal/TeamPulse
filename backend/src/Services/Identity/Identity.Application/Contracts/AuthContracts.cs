using System;

namespace Identity.Application.Contracts;
public sealed record RegisterRequest(string Email, string Password, string DisplayName, string OrganizationId, string? TeamId = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, EmployeeSummary Employee);
public sealed record EmployeeSummary(Guid EmployeeId, string Email, string DisplayName, string OrganizationId, string? TeamId, string Role);
