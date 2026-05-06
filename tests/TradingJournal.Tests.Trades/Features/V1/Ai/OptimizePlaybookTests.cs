using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Playbook;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class OptimizePlaybookValidatorTests
{
    private readonly OptimizePlaybook.Validator _validator = new();

    [Fact]
    public void Validate_ToDateBeforeFromDate_ReturnsInvalid()
    {
        DateTime fromDate = new(2026, 5, 1);
        DateTime toDate = new(2026, 4, 30);

        var result = _validator.Validate(new OptimizePlaybook.Request(fromDate, toDate));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("to date", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class OptimizePlaybookHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsRecommendations_ReturnsSuccess()
    {
        DateTime fromDate = new(2026, 1, 1);
        DateTime toDate = new(2026, 4, 30);

        PlaybookOptimizationResultDto expected = new()
        {
            Summary = "Two setups deserve focus, while one should be retired if performance does not recover.",
            SampleSize = 3,
            Recommendations =
            [
                new PlaybookOptimizationRecommendationDto
                {
                    SetupId = 7,
                    SetupName = "London FVG",
                    Action = "prioritize",
                    Rationale = "It leads the book on expectancy and profit factor with adequate sample size.",
                    Recommendation = "Allocate more reps to London FVG and keep execution criteria tight.",
                    Confidence = 0.91m,
                    TotalTrades = 24,
                    WinRate = 58.3m,
                    TotalPnl = 1260m,
                    Expectancy = 52.5m,
                    AvgRiskReward = 2.1m,
                    Grade = "B"
                }
            ]
        };

        _aiService
            .Setup(service => service.OptimizePlaybookAsync(
                It.Is<PlaybookOptimizationRequestDto>(dto => dto.UserId == 19 && dto.FromDate == fromDate && dto.ToDate == toDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new OptimizePlaybook.Handler(_aiService.Object);

        var result = await handler.Handle(new OptimizePlaybook.Request(fromDate, toDate, 19), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.OptimizePlaybookAsync(It.IsAny<PlaybookOptimizationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlaybookOptimizationResultDto?)null);

        var handler = new OptimizePlaybook.Handler(_aiService.Object);

        var result = await handler.Handle(new OptimizePlaybook.Request(null, null, 5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}