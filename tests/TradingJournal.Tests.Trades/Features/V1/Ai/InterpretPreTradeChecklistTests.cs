using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Validation;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class InterpretPreTradeChecklistValidatorTests
{
    private readonly InterpretPreTradeChecklist.Validator _validator = new();

    [Fact]
    public void Validate_EmptyInput_ReturnsInvalid()
    {
        var result = _validator.Validate(new InterpretPreTradeChecklist.Request(3, string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("input", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class InterpretPreTradeChecklistHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsMatches_ReturnsSuccess()
    {
        PreTradeChecklistInterpretationResultDto expected = new()
        {
            ChecklistModelId = 4,
            Summary = "Two criteria are clearly supported.",
            Confidence = 0.81m,
            SuggestedChecklistIds = [12, 18],
            Matches =
            [
                new PreTradeChecklistInterpretationMatchDto
                {
                    ChecklistId = 12,
                    ChecklistName = "Liquidity sweep confirmed",
                    Category = "Market Structure",
                    Rationale = "The trader explicitly mentioned the sweep.",
                    Confidence = 0.9m
                }
            ]
        };

        _aiService
            .Setup(service => service.InterpretPreTradeChecklistAsync(
                It.Is<PreTradeChecklistInterpretationRequestDto>(dto => dto.ChecklistModelId == 4 && dto.Input == "sweep happened, displacement confirmed" && dto.UserId == 9),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new InterpretPreTradeChecklist.Handler(_aiService.Object);

        var result = await handler.Handle(new InterpretPreTradeChecklist.Request(4, "sweep happened, displacement confirmed", 9), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.InterpretPreTradeChecklistAsync(It.IsAny<PreTradeChecklistInterpretationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PreTradeChecklistInterpretationResultDto?)null);

        var handler = new InterpretPreTradeChecklist.Handler(_aiService.Object);

        var result = await handler.Handle(new InterpretPreTradeChecklist.Request(4, "notes", 3), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}