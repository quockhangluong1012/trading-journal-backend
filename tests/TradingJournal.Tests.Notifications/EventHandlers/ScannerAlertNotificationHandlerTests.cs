using TradingJournal.Modules.Notifications.EventHandlers;

namespace TradingJournal.Tests.Notifications.EventHandlers;

public class ScannerAlertNotificationHandlerTests
{
    private sealed record Captured(
        int UserId,
        string Title,
        string Message,
        NotificationType Type,
        NotificationPriority Priority,
        string? Metadata,
        string? ActionUrl);

    private static (ScannerAlertNotificationHandler Handler, Func<Captured?> Get) CreateSut()
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
            .ReturnsAsync(1);

        var handler = new ScannerAlertNotificationHandler(
            service.Object,
            NullLogger<ScannerAlertNotificationHandler>.Instance);

        return (handler, () => captured);
    }

    private static ScannerAlertEvent Event(int confluenceScore) => new(
        EventId: Guid.NewGuid(),
        UserId: 42,
        Symbol: "EURUSD",
        PatternType: "OrderBlock",
        Timeframe: "H1",
        Price: 1.2345m,
        Description: "Bullish order block detected",
        ConfluenceScore: confluenceScore);

    [Theory]
    [InlineData(3, NotificationPriority.High)]
    [InlineData(5, NotificationPriority.High)]
    [InlineData(2, NotificationPriority.Normal)]
    [InlineData(1, NotificationPriority.Low)]
    [InlineData(0, NotificationPriority.Low)]
    public async Task Handle_MapsConfluenceScoreToPriority(int score, NotificationPriority expected)
    {
        (ScannerAlertNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event(score), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.Equal(expected, c!.Priority);
    }

    [Fact]
    public async Task Handle_SetsScannerAlertTypeAndTitleContainsPatternAndSymbol()
    {
        (ScannerAlertNotificationHandler handler, Func<Captured?> get) = CreateSut();

        await handler.Handle(Event(3), CancellationToken.None);

        Captured? c = get();
        Assert.NotNull(c);
        Assert.Equal(NotificationType.ScannerAlert, c!.Type);
        Assert.Contains("OrderBlock", c.Title);
        Assert.Contains("EURUSD", c.Title);
        Assert.Equal(42, c.UserId);
        Assert.Equal("Bullish order block detected", c.Message);
        Assert.Contains("/scanner", c.ActionUrl);
    }
}
