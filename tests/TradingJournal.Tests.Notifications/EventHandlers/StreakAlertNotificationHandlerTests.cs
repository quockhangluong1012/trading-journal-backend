using TradingJournal.Modules.Notifications.EventHandlers;

namespace TradingJournal.Tests.Notifications.EventHandlers;

public class StreakAlertNotificationHandlerTests
{
    private sealed record Captured(
        int UserId,
        string Title,
        string Message,
        NotificationType Type,
        NotificationPriority Priority,
        string? Metadata,
        string? ActionUrl);

    private static (StreakAlertNotificationHandler Handler, Func<Captured?> Get) CreateSut()
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
            .ReturnsAsync(7);

        var handler = new StreakAlertNotificationHandler(
            service.Object,
            NullLogger<StreakAlertNotificationHandler>.Instance);

        return (handler, () => captured);
    }

    private static StreakAlertEvent Event(string streakType, bool isNewRecord) => new(
        EventId: Guid.NewGuid(),
        UserId: 10,
        StreakType: streakType,
        StreakLength: 4,
        StreakPnl: -120.5m,
        IsNewRecord: isNewRecord,
        Message: "You are on a streak");

    [Fact]
    public async Task Handle_LossStreak_HighPriorityWarningEmoji()
    {
        (StreakAlertNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event("Loss", isNewRecord: false), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.Equal(NotificationPriority.High, c!.Priority);
        Assert.Contains("⚠️", c.Title);
        Assert.Equal(NotificationType.StreakAlert, c.Type);
        Assert.Equal("/psychology", c.ActionUrl);
    }

    [Fact]
    public async Task Handle_WinStreak_NormalPriorityFireEmoji()
    {
        (StreakAlertNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event("Win", isNewRecord: false), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.Equal(NotificationPriority.Normal, c!.Priority);
        Assert.Contains("🔥", c.Title);
        Assert.Equal(NotificationType.StreakAlert, c.Type);
    }

    [Fact]
    public async Task Handle_NewRecord_ChangesTitle()
    {
        (StreakAlertNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event("Win", isNewRecord: false), CancellationToken.None);
        string nonRecordTitle = get()!.Title;

        (StreakAlertNotificationHandler recordHandler, Func<Captured?> getRecord) = CreateSut();
        await recordHandler.Handle(Event("Win", isNewRecord: true), CancellationToken.None);
        string recordTitle = getRecord()!.Title;

        Assert.NotEqual(nonRecordTitle, recordTitle);
        Assert.Contains("Record", recordTitle);
    }
}
