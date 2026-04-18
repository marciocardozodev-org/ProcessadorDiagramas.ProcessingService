using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProcessadorDiagramas.ReportingService.Tests;

public sealed class HealthEndpointsTests : IClassFixture<ReportingServiceWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public HealthEndpointsTests(ReportingServiceWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOk()
    {
        var response = await _httpClient.GetAsync("/health");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoot_ShouldDescribeServiceRole()
    {
        var response = await _httpClient.GetAsync("/");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        payload.Should().Contain("ProcessadorDiagramas.ReportingService");
        payload.Should().Contain("reporting-api");
    }
}
