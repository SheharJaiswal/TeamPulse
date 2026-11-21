using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Contracts;

namespace Identity.Application.Abstractions;

public interface IEmployeeInvitationActivationService
{
    Task<EmployeeInvitationActivationResult> ActivateAsync(
        ActivateEmployeeInvitationRequest request,
        CancellationToken cancellationToken);
}
