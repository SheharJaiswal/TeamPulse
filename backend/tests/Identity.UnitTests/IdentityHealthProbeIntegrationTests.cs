using System.Net;

namespace Identity.UnitTests;

public sealed class IdentityHealthProbeIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public IdentityHealthProbeIntegrationTests(MySqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LivenessRemainsHealthyWhenDatabaseBecomesUnavailable()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        await _fixture.StopAsync();

        try
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);

            var readiness = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
            var readinessBody = await readiness.Content.ReadAsStringAsync();
            Assert.DoesNotContain("teampulse_test_password", readinessBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Server=", readinessBody, StringComparison.OrdinalIgnoreCase);

            var compatibility = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, compatibility.StatusCode);
        }
        finally
        {
            await _fixture.StartAsync();
        }
    }
}
