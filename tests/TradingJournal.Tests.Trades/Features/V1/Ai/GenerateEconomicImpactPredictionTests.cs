using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Economic;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class GenerateEconomicImpactPredictionValidatorTests
{
    private readonly GenerateEconomicImpactPrediction.Validator _validator = new();

    [Fact]
    public void Validate_EmptySymbol_ReturnsInvalid()
    {
        var result = _validator.Validate(new GenerateEconomicImpactPrediction.Request(string.Empty));

        Assert.False(result.IsValid);
    }
}

public sealed class GenerateEconomicImpactPredictionHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsPrediction_ReturnsSuccess()
    {
        _aiService
            .Setup(service => service.GenerateEconomicImpactPredictionAsync(
                It.Is<AiEconomicImpactPredictorRequestDto>(dto => dto.UserId == 12 && dto.Symbol == "EURUSD" && dto.ProximityMinutes == 45),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiEconomicImpactPredictorResultDto
            {
                RiskLevel = "high",
                Summary = "EURUSD has a near-term event headwind and your event-adjacent history is weaker.",
                TradeStance = "Stand aside until after the release window clears.",
                KeyDrivers = ["USD event in 20m"],
                ActionItems = ["Wait for post-release volatility to settle."],
                Confidence = 0.82m,
            });

        var handler = new GenerateEconomicImpactPrediction.Handler(_aiService.Object);

        var result = await handler.Handle(new GenerateEconomicImpactPrediction.Request("EURUSD", 45, 12), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("high", result.Value.RiskLevel);
    }
}