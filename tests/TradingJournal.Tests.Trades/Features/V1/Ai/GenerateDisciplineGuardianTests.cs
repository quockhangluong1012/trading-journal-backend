using Moq;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Discipline;
using TradingJournal.Modules.AiInsights.Services;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class GenerateDisciplineGuardianHandlerTests
{
    private readonly Mock<IDisciplineContextProvider> _disciplineContextProvider = new();
    private readonly Mock<IOpenRouterAIService> _aiService = new();

    [Fact]
    public async Task Handle_WhenAiReturnsGuidance_ReturnsSuccess()
    {
        _disciplineContextProvider
            .Setup(provider => provider.GetDisciplineContextAsync(14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DisciplineGuardianContextDto(76, "High", 3, 4, 2, -180m, DateTime.UtcNow.AddMinutes(30)));

        _aiService
            .Setup(service => service.AnalyzeTiltInterventionAsync(
                It.Is<AiTiltInterventionRequestDto>(dto => dto.UserId == 14 && dto.TiltScore == 76 && dto.RuleBreaksToday == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiTiltInterventionResultDto
            {
                RiskLevel = "high",
                TiltType = "overtrading",
                Title = "Discipline slipping",
                Message = "Step back before taking another impulsive trade.",
                ActionItems = ["Pause for 30 minutes."],
                ShouldNotify = true,
            });

        var handler = new GenerateDisciplineGuardian.Handler(_disciplineContextProvider.Object, _aiService.Object);

        var result = await handler.Handle(new GenerateDisciplineGuardian.Request(14), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("high", result.Value.RiskLevel);
    }

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsFailure()
    {
        _disciplineContextProvider
            .Setup(provider => provider.GetDisciplineContextAsync(14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DisciplineGuardianContextDto(28, "Elevated", 0, 1, 0, 40m, null));

        _aiService
            .Setup(service => service.AnalyzeTiltInterventionAsync(It.IsAny<AiTiltInterventionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiTiltInterventionResultDto?)null);

        var handler = new GenerateDisciplineGuardian.Handler(_disciplineContextProvider.Object, _aiService.Object);

        var result = await handler.Handle(new GenerateDisciplineGuardian.Request(14), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }
}