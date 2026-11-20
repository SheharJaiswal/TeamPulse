using Identity.Domain.Entities;

namespace Identity.UnitTests;

public sealed class EmployeeLifecycleDomainTests
{
    [Fact]
    public void ChangeActiveState_WhenStateChanges_IncrementsVersionExactlyOnce()
    {
        var employee = CreateEmployee();

        var changed = employee.ChangeActiveState(false);

        Assert.True(changed);
        Assert.False(employee.IsActive);
        Assert.Equal(1, employee.LifecycleVersion);

        changed = employee.ChangeActiveState(true);

        Assert.True(changed);
        Assert.True(employee.IsActive);
        Assert.Equal(2, employee.LifecycleVersion);
    }

    [Fact]
    public void ChangeActiveState_WhenAlreadyInDesiredState_DoesNotIncrementVersion()
    {
        var employee = CreateEmployee();

        var changed = employee.ChangeActiveState(true);

        Assert.False(changed);
        Assert.True(employee.IsActive);
        Assert.Equal(0, employee.LifecycleVersion);
    }

    private static Employee CreateEmployee() => new(
        Guid.NewGuid(), "employee@teampulse.test", "EMPLOYEE@TEAMPULSE.TEST",
        "Lifecycle Test", "hash", "org-test", "team-test", "Employee");
}
