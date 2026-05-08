using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Playbook;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class GenerateTradingSetupValidatorTests
{
    private readonly GenerateTradingSetup.Validator _validator = new();

    [Fact]
    public void Validate_EmptyPrompt_ReturnsInvalid()
    {
        var result = _validator.Validate(new GenerateTradingSetup.Request(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("prompt", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class GenerateTradingSetupHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsSetupPreview_ReturnsSuccess()
    {
        TradingSetupGenerationResultDto expected = new()
        {
            Summary = "A concise NY session reversal setup.",
            Name = "ICT Venom Model",
            Description = "Liquidity sweep and reversal setup.",
            Nodes =
            [
                new TradingSetupGenerationNodeDto { Id = "start-1", Kind = "start", X = 140, Y = 80, Title = "Pre-market map" },
                new TradingSetupGenerationNodeDto { Id = "step-1", Kind = "step", X = 420, Y = 80, Title = "Wait for sweep" }
            ],
            Edges =
            [
                new TradingSetupGenerationEdgeDto { Id = "edge-1", Source = "start-1", Target = "step-1" }
            ],
            Confidence = 0.85m
        };

        _aiService
            .Setup(service => service.GenerateTradingSetupAsync(
                It.Is<TradingSetupGenerationRequestDto>(dto => dto.Prompt == "build venom model" && dto.MaxNodes == 8 && dto.DedupeAgainstExisting && dto.UserId == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GenerateTradingSetup.Handler(_aiService.Object);

        var result = await handler.Handle(new GenerateTradingSetup.Request("build venom model", 8, true, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.GenerateTradingSetupAsync(It.IsAny<TradingSetupGenerationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradingSetupGenerationResultDto?)null);

        var handler = new GenerateTradingSetup.Handler(_aiService.Object);

        var result = await handler.Handle(new GenerateTradingSetup.Request("build venom model", 8, true, 11), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}