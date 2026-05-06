using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Risk;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class GenerateRiskAdviceHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsAdvice_ReturnsSuccess()
    {
        AiRiskAdvisorResultDto expected = new()
        {
            RiskLevel = "high",
            Summary = "Daily losses and open exposure both justify smaller size.",
            PositionSizingAdvice = "Cut your next position size in half until daily loss usage falls below 50%.",
            KeyRisks = ["Daily loss usage is elevated", "Open exposure is near the configured cap"],
            ActionItems = ["Reduce size", "Avoid adding new positions"],
            ShouldReduceRisk = true,
            Confidence = 0.86m,
        };

        _aiService
            .Setup(service => service.GenerateRiskAdvisorAsync(
                It.Is<AiRiskAdvisorRequestDto>(dto => dto.UserId == 27),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GenerateRiskAdvice.Handler(_aiService.Object);

        var result = await handler.Handle(new GenerateRiskAdvice.Request(27), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.GenerateRiskAdvisorAsync(It.IsAny<AiRiskAdvisorRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiRiskAdvisorResultDto?)null);

        var handler = new GenerateRiskAdvice.Handler(_aiService.Object);

        var result = await handler.Handle(new GenerateRiskAdvice.Request(11), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}