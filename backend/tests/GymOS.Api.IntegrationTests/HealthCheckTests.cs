using System.Net;
using GymOS.Api.IntegrationTests.TestSupport;
using Shouldly;

namespace GymOS.Api.IntegrationTests;

public class HealthCheckTests(GymOsWebApplicationFactory factory) : IClassFixture<GymOsWebApplicationFactory>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy_against_a_real_database()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
