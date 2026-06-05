using TradingJournal.Modules.Notifications.EventHandlers;

namespace TradingJournal.Tests.Notifications.EventHandlers;

public class TiltCircuitBreakerNotificationHandlerTests
{
    private sealed record Captured(
        int UserId,
        string Title,
        string Message,
        NotificationType Type,
        NotificationPriority Priority,
        string? Metadata,
        string? ActionUrl);

    private static (TiltCircuitBreakerNotificationHandler Handler, Func<Captured?> Get) CreateSut()
    {
        Captured? captured = null;
        var service = new Mock<INotificationService>();
        service
            .Setup(s => s.CreateAndPushAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationType>(), It.IsAny<NotificationPriority>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, string, NotificationType, NotificationPriority, string?, string?, CancellationToken>(
                (uid, title, msg, type, prio, meta, url, _) =>
                    captured = new Captured(uid, title, msg, type, prio, meta, url))
            .ReturnsAsync(3);

        var handler = new TiltCircuitBreakerNotificationHandler(
            service.Object,
            NullLogger<TiltCircuitBreakerNotificationHandler>.Instance);

        return (handler, () => captured);
    }

    private static TiltCircuitBreakerEvent Event(
        int consecutiveLosses = 3,
        int tradesLastHour = 5,
        int ruleBreaksToday = 1,
        decimal todayPnl = -250m) => new(
        EventId: Guid.NewGuid(),
        UserId: 99,
        TiltScore: 85,
        TiltLevel: "Severe",
        ConsecutiveLosses: consecutiveLosses,
        TradesLastHour: tradesLastHour,
        RuleBreaksToday: ruleBreaksToday,
        TodayPnl: todayPnl,
        CooldownUntil: new DateTime(2026, 6, 5, 14, 30, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Handle_IsCriticalTiltWarning()
    {
        (TiltCircuitBreakerNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event(), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.Equal(NotificationPriority.Critical, c!.Priority);
        Assert.Equal(NotificationType.TiltWarning, c.Type);
        Assert.Equal("/psychology", c.ActionUrl);
    }

    [Fact]
    public async Task Handle_MessageIncludesConsecutiveLossesOvertradingAndCooldown()
    {
        (TiltCircuitBreakerNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event(consecutiveLosses: 3, tradesLastHour: 5), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.Contains("3 consecutive losses", c!.Message);
        Assert.Contains("overtrading", c.Message);
        Assert.Contains("cooldown until", c.Message);
    }

    [Fact]
    public async Task Handle_NoOvertrading_WhenTradesLastHourNotAboveThree()
    {
        (TiltCircuitBreakerNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event(tradesLastHour: 3), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.DoesNotContain("overtrading", c!.Message);
    }
}
