using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Contracts;

namespace Identity.Application.Abstractions;

public interface IEmployeeInvitationService
{
    Task<EmployeeInvitationCommandResult> CreateAsync(
        Guid invitedByEmployeeId,
        CreateEmployeeInvitationRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeInvitationSummary?> GetAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<EmployeeInvitationCommandResult> ResendAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<EmployeeInvitationCommandResult> CancelAsync(
        Guid invitationId,
        CancellationToken cancellationToken);
}
