using System.Net;
using System.Net.Http.Json;
using TradingJournal.Tests.Integration.Infrastructure;

namespace TradingJournal.Tests.Integration.AiInsights;

public sealed class AnalyzeChartScreenshotEndpointTests(ValidationOnlyWebFactory factory)
    : IClassFixture<ValidationOnlyWebFactory>
{
    [Fact]
    public async Task AnalyzeChartScreenshot_WithInvalidScreenshotUrl_ReturnsBadRequest_WithoutInvokingAiService()
    {
        // Arrange
        factory.FakeOpenRouterAiService.Reset();
        HttpClient client = factory.CreateAuthenticatedClient();
        var payload = new
        {
            asset = "EURUSD",
            position = "Long",
            entryPrice = 1.0850m,
            stopLoss = 1.0800m,
            targetTier1 = 1.0900m,
            tradingZone = "London",
            notes = "Invalid screenshot source should fail validation.",
            screenshots = new[] { "http://example.com/chart.png" }
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/ai-validation/analyze-chart", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.FakeOpenRouterAiService.AnalyzeChartScreenshotCalls);
    }
}