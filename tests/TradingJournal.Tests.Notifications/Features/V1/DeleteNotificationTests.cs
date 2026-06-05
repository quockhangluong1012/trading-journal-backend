using TradingJournal.Tests.Notifications.Helpers;

namespace TradingJournal.Tests.Notifications.Features.V1;

public class DeleteNotificationTests
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

        var handler = new DeleteNotification.Handler(context.Object, hub.Object, cache.Object);

        Result<bool> result = await handler.Handle(
            new DeleteNotification.Request(999) { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(Error.NotFound, result.Errors);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SoftDeletesAndSaves()
    {
        var notification = new Notification { Id = 5, UserId = 1, IsRead = true, IsDisabled = false };
        Mock<INotificationDbContext> context = CreateContext([notification]);
        Mock<Microsoft.AspNetCore.SignalR.IHubContext<Modules.Notifications.Hubs.NotificationHub>> hub =
            SignalRMockHelper.CreateHubContext(out _);
        var cache = new Mock<ICacheRepository>();

        var handler = new DeleteNotification.Handler(context.Object, hub.Object, cache.Object);

        Result<bool> result = await handler.Handle(
            new DeleteNotification.Request(5) { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(notification.IsDisabled);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        // read notification => no cache invalidation
        cache.Verify(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeletingUnread_ClearsCache()
    {
        var notification = new Notification { Id = 5, UserId = 1, IsRead = false, IsDisabled = false };
        Mock<INotificationDbContext> context = CreateContext([notification]);
        Mock<Microsoft.AspNetCore.SignalR.IHubContext<Modules.Notifications.Hubs.NotificationHub>> hub =
            SignalRMockHelper.CreateHubContext(out _);
        var cache = new Mock<ICacheRepository>();

        var handler = new DeleteNotification.Handler(context.Object, hub.Object, cache.Object);

        Result<bool> result = await handler.Handle(
            new DeleteNotification.Request(5) { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(notification.IsDisabled);
        cache.Verify(c => c.RemoveCache(
            CacheKeys.UnreadCountForUser(1), It.IsAny<CancellationToken>()), Times.Once);
    }
}
