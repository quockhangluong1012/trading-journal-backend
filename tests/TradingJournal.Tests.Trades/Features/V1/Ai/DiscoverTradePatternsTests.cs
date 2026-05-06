using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Patterns;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class DiscoverTradePatternsValidatorTests
{
    private readonly DiscoverTradePatterns.Validator _validator = new();

    [Fact]
    public void Validate_ToDateBeforeFromDate_ReturnsInvalid()
    {
        DateTime fromDate = new(2026, 5, 1);
        DateTime toDate = new(2026, 4, 30);

        var result = _validator.Validate(new DiscoverTradePatterns.Request(fromDate, toDate));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("to date", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class DiscoverTradePatternsHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsPatterns_ReturnsSuccess()
    {
        DateTime fromDate = new(2026, 4, 1);
        DateTime toDate = new(2026, 4, 30);

        TradePatternDiscoveryResultDto expected = new()
        {
            Summary = "Your London session setups are materially stronger than New York this month.",
            SampleSize = 18,
            Patterns =
            [
                new DiscoveredTradePatternDto
                {
                    Title = "London FVG strength",
                    Category = "session",
                    Description = "London trades with FVG confluence outperformed other combinations.",
                    Evidence = "8 wins from 10 London FVG trades.",
                    Confidence = 0.89
                }
            ],
            ActionItems = ["Prioritize London FVG setups while this edge persists."]
        };

        _aiService
            .Setup(service => service.DiscoverTradePatternsAsync(
                It.Is<TradePatternDiscoveryRequestDto>(dto => dto.UserId == 19 && dto.FromDate == fromDate && dto.ToDate == toDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new DiscoverTradePatterns.Handler(_aiService.Object);

        var result = await handler.Handle(new DiscoverTradePatterns.Request(fromDate, toDate, 19), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.DiscoverTradePatternsAsync(It.IsAny<TradePatternDiscoveryRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradePatternDiscoveryResultDto?)null);

        var handler = new DiscoverTradePatterns.Handler(_aiService.Object);

        var result = await handler.Handle(new DiscoverTradePatterns.Request(null, null, 5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}