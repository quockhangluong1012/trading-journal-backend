using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Validation;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class AnalyzeChartScreenshotValidatorTests
{
    private readonly AnalyzeChartScreenshot.Validator _validator = new();

    [Fact]
    public void Validate_EmptyScreenshots_ReturnsInvalid()
    {
        var result = _validator.Validate(new AnalyzeChartScreenshot.Request("EURUSD", "Long", null, null, null, null, null, [], 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("screenshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var result = _validator.Validate(new AnalyzeChartScreenshot.Request("EURUSD", "Flat", null, null, null, null, null, ["data:image/png;base64,iVBORw0KGgo="], 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Position", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TooManyScreenshots_ReturnsInvalid()
    {
        var screenshots = Enumerable.Repeat("data:image/png;base64,iVBORw0KGgo=", AnalyzeChartScreenshot.MaxScreenshotCount + 1).ToList();

        var result = _validator.Validate(new AnalyzeChartScreenshot.Request("EURUSD", "Long", null, null, null, null, null, screenshots, 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Upload between", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NonHttpsRemoteScreenshot_ReturnsInvalid()
    {
        var result = _validator.Validate(new AnalyzeChartScreenshot.Request("EURUSD", "Long", null, null, null, null, null, ["http://example.com/chart.png"], 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("HTTPS", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AnalyzeChartScreenshotHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsAnalysis_ReturnsSuccess()
    {
        ChartScreenshotAnalysisResultDto expected = new()
        {
            Summary = "Bullish intraday structure with discount re-entry context, but confirmation is still incomplete.",
            MarketStructure = "Bullish BOS on the visible intraday swing.",
            AmdPhase = "Manipulation transitioning into distribution.",
            PremiumDiscount = "Discount",
            ConfidenceScore = 0.84m,
            KeyLevels = ["1.0840 discount array", "1.0862 buy-side liquidity"],
            DetectedConfluences = ["Fair value gap retest", "Order block inside discount"],
            Warnings = ["Liquidity sweep confirmation is not fully visible."],
            SuggestedActions = ["Wait for a stronger bullish confirmation candle before entry."]
        };

        _aiService
            .Setup(service => service.AnalyzeChartScreenshotAsync(
                It.Is<ChartScreenshotAnalysisRequestDto>(dto => dto.Asset == "EURUSD" && dto.Screenshots.Count == 1 && dto.UserId == 19),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new AnalyzeChartScreenshot.Handler(_aiService.Object);

        var result = await handler.Handle(new AnalyzeChartScreenshot.Request(
            "EURUSD",
            "Long",
            1.0842m,
            1.0825m,
            1.0875m,
            "London",
            "Watching for discount entry after sweep.",
            ["data:image/png;base64,ZmFrZQ=="],
            19), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.AnalyzeChartScreenshotAsync(It.IsAny<ChartScreenshotAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChartScreenshotAnalysisResultDto?)null);

        var handler = new AnalyzeChartScreenshot.Handler(_aiService.Object);

        var result = await handler.Handle(new AnalyzeChartScreenshot.Request(
            "EURUSD",
            "Long",
            null,
            null,
            null,
            null,
            null,
            ["data:image/png;base64,ZmFrZQ=="],
            5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}