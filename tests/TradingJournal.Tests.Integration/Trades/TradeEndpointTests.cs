using System.Net;
using System.Net.Http.Json;
using TradingJournal.Tests.Integration.Infrastructure;

namespace TradingJournal.Tests.Integration.Trades;

/// <summary>
/// Integration tests for Trade CRUD endpoints.
/// Validates the full pipeline: HTTP → Carter → MediatR → EF Core → SQL Server.
/// </summary>
public sealed class TradeEndpointTests(TradingJournalWebFactory factory)
    : IClassFixture<TradingJournalWebFactory>
{
    [Fact]
    public async Task GetTrades_Authenticated_ReturnsOk()
    {
        // Arrange
        HttpClient client = factory.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/trades?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTrades_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/trades?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_Authenticated_ReturnsOk()
    {
        // Arrange
        HttpClient client = factory.CreateAuthenticatedClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard?filter=AllTime");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }
}
