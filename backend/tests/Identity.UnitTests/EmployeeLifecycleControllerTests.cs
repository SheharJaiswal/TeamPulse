using System.Security.Claims;
using Identity.API.Controllers;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;

namespace Identity.UnitTests;

public sealed class EmployeeLifecycleControllerTests
{
    [Fact]
    public async Task Deactivate_RejectsPresentEmptyCorrelationHeaderEvenWhenItHasZeroParsedValues()
    {
        var lifecycleService = new Mock<IEmployeeLifecycleService>(MockBehavior.Strict);
        var controller = new EmployeeLifecycleController(lifecycleService.Object);
        var httpContext = new DefaultHttpContext();
        var actorId = Guid.NewGuid();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", actorId.ToString()), new Claim(ClaimTypes.Role, "HrAdmin")],
            authenticationType: "Test"));
        httpContext.Request.Headers["Idempotency-Key"] = "empty-correlation-header";
        httpContext.Request.Headers.Add("X-Correlation-ID", StringValues.Empty);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        Assert.True(httpContext.Request.Headers.ContainsKey("X-Correlation-ID"));
        Assert.Equal(0, httpContext.Request.Headers["X-Correlation-ID"].Count);

        var result = await controller.Deactivate(
            Guid.NewGuid(),
            new EmployeeLifecycleRequest("valid-reason", null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        lifecycleService.Verify(
            service => service.ExecuteAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
