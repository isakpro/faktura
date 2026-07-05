using System.Net;
using Xunit;

namespace Faktura.Api.Tests;

public class ObservabilityEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public ObservabilityEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_liveness_is_public_and_healthy()
    {
        var resp = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Healthy", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApi_document_is_served_and_covers_the_api()
    {
        var resp = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("/api/invoices", json);
        Assert.Contains("/api/articles", json);
        Assert.Contains("/api/auth/register", json);
    }

    [Fact]
    public async Task Scalar_reference_ui_is_served()
    {
        var resp = await _factory.CreateClient().GetAsync("/scalar/v1");
        // Scalar svarar med HTML-referenssidan (v1 = dokumentnamnet).
        Assert.True(resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect,
            $"Oväntad status: {resp.StatusCode}");
    }
}
