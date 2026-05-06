using Moq;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Features.V1.Digest;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class GenerateWeeklyDigestNotificationHandlerTests
{
    private readonly Mock<IOpenRouterAIService> _aiService = new();
    private readonly Mock<IEventBus> _eventBus = new();

    [Fact]
    public async Task Handle_WhenDigestExists_PublishesNotificationEvent()
    {
        _aiService
            .Setup(service => service.GenerateWeeklyDigestAsync(
                It.Is<AiWeeklyDigestRequestDto>(dto => dto.UserId == 31),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiWeeklyDigestResultDto
            {
                Headline = "Weekly AI Digest",
                Summary = "You traded less but cleaner this week.",
                KeyWins = ["Rule adherence improved"],
                KeyRisks = ["One late-week rule break"],
                FocusForNextWeek = "Protect the strong start by keeping trade count selective.",
                ActionItems = ["Cap yourself at three A-setups per day."],
            });

        var handler = new GenerateWeeklyDigestNotification.Handler(_aiService.Object, _eventBus.Object);

        var result = await handler.Handle(new GenerateWeeklyDigestNotification.Request(31), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _eventBus.Verify(bus => bus.PublishAsync(
            It.Is<AiWeeklyDigestGeneratedEvent>(evt => evt.UserId == 31 && evt.Headline == "Weekly AI Digest"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDigestMissing_ReturnsFailure()
    {
        _aiService
            .Setup(service => service.GenerateWeeklyDigestAsync(It.IsAny<AiWeeklyDigestRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiWeeklyDigestResultDto?)null);

        var handler = new GenerateWeeklyDigestNotification.Handler(_aiService.Object, _eventBus.Object);

        var result = await handler.Handle(new GenerateWeeklyDigestNotification.Request(31), CancellationToken.None);

        Assert.False(result.IsSuccess);
        _eventBus.Verify(bus => bus.PublishAsync(It.IsAny<AiWeeklyDigestGeneratedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}