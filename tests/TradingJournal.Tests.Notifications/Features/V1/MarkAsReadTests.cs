using TradingJournal.Tests.Notifications.Helpers;

namespace TradingJournal.Tests.Notifications.Features.V1;

public class MarkAsReadTests
{
    private static Mock<INotificationDbContext> CreateContext(List<Notification> notifications)
    {
        Mock<DbSet<Notification>> dbSet = DbSetMockHelper.CreateMockDbSet(notifications);
        var context = new Mock<INotificationDbContext>();
        context.Setup(c => c.Notifications).Returns(dbSet.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return context;
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsFailure()
    {
        Mock<INotificationDbContext> context = CreateContext([]);
        Mock<Microsoft.AspNetCore.SignalR.IHubContext<Modules.Notifications.Hubs.NotificationHub>> hub =
            SignalRMockHelper.CreateHubContext(out _);
        var cache = new Mock<ICacheRepository>();

        var handler = new MarkAsRead.Handler(context.Object, hub.Object, cache.Object);

        Result<bool> result = await handler.Handle(
            new MarkAsRead.Request(123) { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(Error.NotFound, result.Errors);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyRead_IsIdempotent_NoSaveChanges()
    {
        var notification = new Notification { Id = 5, UserId = 1, IsRead = true };
        Mock<INotificationDbContext> context = CreateContext([notification]);
        Mock<Microsoft.AspNetCore.SignalR.IHubContext<Modules.Notifications.Hubs.NotificationHub>> hub =
            SignalRMockHelper.CreateHubContext(out _);
        var cache = new Mock<ICacheRepository>();

        var handler = new MarkAsRead.Handler(context.Object, hub.Object, cache.Object);

        Result<bool> result = await handler.Handle(
            new MarkAsRead.Request(5) { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Unread_SetsReadSavesAndClearsCache()
    {
        var notification = new Notification { Id = 5, UserId = 1, IsRead = false };
        Mock<INotificationDbContext> context = CreateContext([notification]);
        Mock<Microsoft.AspNetCore.SignalR.IHubContext<Modules.Notifications.Hubs.NotificationHub>> hub =
            SignalRMockHelper.CreateHubContext(out _);
        var cache = new Mock<ICacheRepository>();

        var handler = new MarkAsRead.Handler(context.Object, hub.Object, cache.Object);

        Result<bool> result = await handler.Handle(
            new MarkAsRead.Request(5) { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(
            CacheKeys.UnreadCountForUser(1), It.IsAny<CancellationToken>()), Times.Once);
    }
}
