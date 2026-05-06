using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.EventHandlers;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class TiltSnapshotUpdatedAiHandlerTests
{
    [Fact]
    public async Task Handle_WhenAiRequestsNotification_PublishesAiTiltEvent()
    {
        var aiService = new Mock<IOpenRouterAIService>();
        var eventBus = new Mock<IEventBus>();

        aiService
            .Setup(service => service.AnalyzeTiltInterventionAsync(
                It.Is<AiTiltInterventionRequestDto>(dto => dto.UserId == 7 && dto.TiltScore == 78),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiTiltInterventionResultDto
            {
                RiskLevel = "high",
                TiltType = "revenge_trading",
                Title = "Pause before the next trade",
                Message = "Recent behavior looks like revenge trading.",
                ActionItems = ["Stand down for 30 minutes."],
                ShouldNotify = true,
            });

        var handler = new TiltSnapshotUpdatedAiHandler(
            aiService.Object,
            eventBus.Object,
            NullLogger<TiltSnapshotUpdatedAiHandler>.Instance);

        await handler.Handle(
            new TiltSnapshotUpdatedEvent(Guid.NewGuid(), 7, 78, "High", 3, 4, 1, -220m, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow),
            CancellationToken.None);

        eventBus.Verify(bus => bus.PublishAsync(
            It.Is<AiTiltInterventionDetectedEvent>(evt => evt.UserId == 7 && evt.RiskLevel == "high" && evt.TiltType == "revenge_trading"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAiThrows_DoesNotPublishEvent()
    {
        var aiService = new Mock<IOpenRouterAIService>();
        var eventBus = new Mock<IEventBus>();

        aiService
            .Setup(service => service.AnalyzeTiltInterventionAsync(It.IsAny<AiTiltInterventionRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OpenRouter unavailable"));

        var handler = new TiltSnapshotUpdatedAiHandler(
            aiService.Object,
            eventBus.Object,
            NullLogger<TiltSnapshotUpdatedAiHandler>.Instance);

        await handler.Handle(
            new TiltSnapshotUpdatedEvent(Guid.NewGuid(), 7, 78, "High", 3, 4, 1, -220m, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow),
            CancellationToken.None);

        eventBus.Verify(bus => bus.PublishAsync(It.IsAny<AiTiltInterventionDetectedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}