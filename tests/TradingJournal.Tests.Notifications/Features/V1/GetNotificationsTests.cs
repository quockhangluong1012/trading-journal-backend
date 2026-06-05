using TradingJournal.Tests.Notifications.Helpers;

namespace TradingJournal.Tests.Notifications.Features.V1;

public class GetNotificationsTests
{
    private static Mock<INotificationDbContext> CreateContext(List<Notification> notifications)
    {
        Mock<DbSet<Notification>> dbSet = DbSetMockHelper.CreateMockDbSet(notifications);
        var context = new Mock<INotificationDbContext>();
        context.Setup(c => c.Notifications).Returns(dbSet.Object);
        return context;
    }

    private static List<Notification> Sample() =>
    [
        new() { Id = 1, UserId = 1, IsRead = false, IsDisabled = false, CreatedDate = new DateTime(2026, 1, 1) },
        new() { Id = 2, UserId = 1, IsRead = true, IsDisabled = false, CreatedDate = new DateTime(2026, 1, 3) },
        new() { Id = 3, UserId = 1, IsRead = false, IsDisabled = false, CreatedDate = new DateTime(2026, 1, 2) },
        new() { Id = 4, UserId = 1, IsRead = false, IsDisabled = true, CreatedDate = new DateTime(2026, 1, 5) },  // disabled
        new() { Id = 5, UserId = 2, IsRead = false, IsDisabled = false, CreatedDate = new DateTime(2026, 1, 4) }, // other user
    ];

    [Fact]
    public async Task Handle_FiltersByUserAndNotDisabled_OrderedByCreatedDateDesc()
    {
        Mock<INotificationDbContext> context = CreateContext(Sample());
        var handler = new GetNotifications.Handler(context.Object);

        Result<List<NotificationDto>> result = await handler.Handle(
            new GetNotifications.Request { UserId = 1, Page = 1, PageSize = 20 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // user 1, not disabled => ids 1,2,3; ordered by CreatedDate desc => 2 (Jan3), 3 (Jan2), 1 (Jan1)
        Assert.Equal([2, 3, 1], result.Value.Select(n => n.Id).ToList());
    }

    [Fact]
    public async Task Handle_UnreadOnly_ExcludesRead()
    {
        Mock<INotificationDbContext> context = CreateContext(Sample());
        var handler = new GetNotifications.Handler(context.Object);

        Result<List<NotificationDto>> result = await handler.Handle(
            new GetNotifications.Request { UserId = 1, UnreadOnly = true, Page = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        // unread, user 1, not disabled => 3 (Jan2), 1 (Jan1)
        Assert.Equal([3, 1], result.Value.Select(n => n.Id).ToList());
        Assert.All(result.Value, n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task Handle_Pagination_SkipsAndTakes()
    {
        Mock<INotificationDbContext> context = CreateContext(Sample());
        var handler = new GetNotifications.Handler(context.Object);

        // Page 2 with PageSize 1 => skip 1, take 1 => second item in desc order (id 3)
        Result<List<NotificationDto>> result = await handler.Handle(
            new GetNotifications.Request { UserId = 1, Page = 2, PageSize = 1 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        NotificationDto dto = Assert.Single(result.Value);
        Assert.Equal(3, dto.Id);
    }
}
