using System.Net;
using System.Net.Http.Json;
using TradingJournal.Tests.Integration.Infrastructure;

namespace TradingJournal.Tests.Integration.AiInsights;

public sealed class AiSetupLinkedTradeFlowTests(TradingJournalWebFactory factory)
    : IClassFixture<TradingJournalWebFactory>
{
    [Fact]
    public async Task GenerateSetup_CreateLinkedTrade_ExposesSetupInAnalyticsAndReview()
    {
        // Arrange
        HttpClient traderClient = factory.CreateAuthenticatedClient(userId: 41);
        HttpClient adminClient = factory.CreateAuthenticatedClient(userId: 7, role: "Admin");
        DateTime tradeTimestamp = DateTime.UtcNow.Date.AddHours(13);

        HttpResponseMessage checklistModelResponse = await traderClient.PostAsJsonAsync("/api/v1/checklist-models", new
        {
            name = "AI Setup Flow",
            description = "Checklist for AI-generated setup validation."
        });

        Assert.Equal(HttpStatusCode.Created, checklistModelResponse.StatusCode);

        ResultEnvelope<int> checklistModelEnvelope = await ReadResultAsync<int>(checklistModelResponse);
        int checklistModelId = checklistModelEnvelope.Value;

        HttpResponseMessage criteriaResponse = await traderClient.PostAsJsonAsync($"/api/v1/checklist-models/{checklistModelId}/criteria", new
        {
            name = "Liquidity sweep confirmed",
            type = 2,
        });

        Assert.Equal(HttpStatusCode.Created, criteriaResponse.StatusCode);

        ResultEnvelope<int> criteriaEnvelope = await ReadResultAsync<int>(criteriaResponse);
        int criteriaId = criteriaEnvelope.Value;

        HttpResponseMessage tradingZoneResponse = await adminClient.PostAsJsonAsync("/api/v1/trading-zones", new
        {
            name = "London Open",
            fromTime = "08:00",
            toTime = "10:00",
            description = "Primary killzone for the AI setup flow test."
        });

        Assert.Equal(HttpStatusCode.Created, tradingZoneResponse.StatusCode);

        ResultEnvelope<int> tradingZoneEnvelope = await ReadResultAsync<int>(tradingZoneResponse);
        int tradingZoneId = tradingZoneEnvelope.Value;

        HttpResponseMessage generationResponse = await traderClient.PostAsJsonAsync("/api/v1/ai-playbook/generate-setup", new
        {
            prompt = "Build a Venom Model setup for the London open with a sweep, displacement, and continuation.",
            maxNodes = 5,
            dedupeAgainstExisting = false,
        });

        Assert.Equal(HttpStatusCode.OK, generationResponse.StatusCode);

        ResultEnvelope<TradingSetupGenerationPayload> generationEnvelope = await ReadResultAsync<TradingSetupGenerationPayload>(generationResponse);
        TradingSetupGenerationPayload generatedSetup = generationEnvelope.Value;

        HttpResponseMessage createSetupResponse = await traderClient.PostAsJsonAsync("/api/v1/trading-setups", new
        {
            name = generatedSetup.Name,
            description = generatedSetup.Description,
            nodes = generatedSetup.Nodes.Select(node => new
            {
                id = node.Id,
                kind = node.Kind,
                x = node.X,
                y = node.Y,
                title = node.Title,
                notes = node.Notes,
            }),
            edges = generatedSetup.Edges.Select(edge => new
            {
                id = edge.Id,
                source = edge.Source,
                target = edge.Target,
                label = edge.Label,
            }),
        });

        Assert.Equal(HttpStatusCode.Created, createSetupResponse.StatusCode);

        ResultEnvelope<int> createSetupEnvelope = await ReadResultAsync<int>(createSetupResponse);
        int setupId = createSetupEnvelope.Value;

        HttpResponseMessage createTradeResponse = await traderClient.PostAsJsonAsync("/api/v1/trade-histories", new
        {
            asset = "EURUSD",
            position = 1,
            entryPrice = 1.0845m,
            targetTier1 = 1.0895m,
            targetTier2 = (decimal?)null,
            targetTier3 = (decimal?)null,
            stopLoss = 1.0815m,
            notes = "Executed the AI-generated Venom setup during London open.",
            date = tradeTimestamp,
            status = 2,
            exitPrice = 1.0890m,
            pnl = 45.5m,
            closedDate = tradeTimestamp.AddHours(1),
            screenshots = Array.Empty<string>(),
            tradeTechnicalAnalysisTags = Array.Empty<int>(),
            emotionTags = Array.Empty<int>(),
            confidenceLevel = 4,
            psychologyNotes = (string?)null,
            tradeHistoryChecklists = new[] { criteriaId },
            tradingZoneId = tradingZoneId,
            tradingSessionId = (int?)null,
            tradingSetupId = setupId,
        });

        Assert.Equal(HttpStatusCode.Created, createTradeResponse.StatusCode);

        ResultEnvelope<int> createTradeEnvelope = await ReadResultAsync<int>(createTradeResponse);
        int tradeId = createTradeEnvelope.Value;

        // Act
        HttpResponseMessage analyticsResponse = await traderClient.GetAsync("/api/v1/analytics/setup-performance?filter=4");
        HttpResponseMessage reviewTradesResponse = await traderClient.PostAsJsonAsync("/api/v1/reviews/trades", new
        {
            fromDate = tradeTimestamp.AddDays(-1),
            toDate = tradeTimestamp.AddDays(1),
            page = 1,
            pageSize = 25,
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, analyticsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewTradesResponse.StatusCode);

        ResultEnvelope<List<SetupPerformanceRow>> analyticsEnvelope = await ReadResultAsync<List<SetupPerformanceRow>>(analyticsResponse);
        SetupPerformanceRow? setupPerformance = analyticsEnvelope.Value.SingleOrDefault(row => row.SetupId == setupId);

        Assert.NotNull(setupPerformance);
        Assert.Equal(generatedSetup.Name, setupPerformance!.SetupName);
        Assert.True(setupPerformance.TotalTrades >= 1);

        ResultEnvelope<PagedValue<ReviewTradeRow>> reviewEnvelope = await ReadResultAsync<PagedValue<ReviewTradeRow>>(reviewTradesResponse);
        ReviewTradeRow? reviewTrade = reviewEnvelope.Value.Values.SingleOrDefault(row => row.Id == tradeId);

        Assert.NotNull(reviewTrade);
        Assert.Equal(generatedSetup.Name, reviewTrade!.TradingSetupName);
    }

    private static async Task<ResultEnvelope<T>> ReadResultAsync<T>(HttpResponseMessage response)
    {
        ResultEnvelope<T>? envelope = await response.Content.ReadFromJsonAsync<ResultEnvelope<T>>();

        Assert.NotNull(envelope);
        Assert.True(envelope!.IsSuccess);
        return envelope;
    }

    private sealed class ResultEnvelope<T>
    {
        public bool IsSuccess { get; set; }

        public T Value { get; set; } = default!;
    }

    private sealed class TradingSetupGenerationPayload
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public List<TradingSetupNodePayload> Nodes { get; set; } = [];

        public List<TradingSetupEdgePayload> Edges { get; set; } = [];
    }

    private sealed class TradingSetupNodePayload
    {
        public string Id { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }

    private sealed class TradingSetupEdgePayload
    {
        public string Id { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string? Label { get; set; }
    }

    private sealed class SetupPerformanceRow
    {
        public int SetupId { get; set; }

        public string SetupName { get; set; } = string.Empty;

        public int TotalTrades { get; set; }
    }

    private sealed class PagedValue<T>
    {
        public List<T> Values { get; set; } = [];

        public int TotalItems { get; set; }

        public bool HasMore { get; set; }
    }

    private sealed class ReviewTradeRow
    {
        public int Id { get; set; }

        public string Asset { get; set; } = string.Empty;

        public string? TradingSetupName { get; set; }
    }
}