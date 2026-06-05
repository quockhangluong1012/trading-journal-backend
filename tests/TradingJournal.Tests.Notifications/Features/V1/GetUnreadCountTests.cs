using TradingJournal.Tests.Notifications.Helpers;

namespace TradingJournal.Tests.Notifications.Features.V1;

public class GetUnreadCountTests
{
    [Fact]
    public async Task Handle_CacheMissFactory_CountsUnreadNotifications()
    {
        var notifications = new List<Notification>
        {
            new() { Id = 1, UserId = 1, IsRead = false, IsDisabled = false },
            new() { Id = 2, UserId = 1, IsRead = false, IsDisabled = false },
            new() { Id = 3, UserId = 1, IsRead = true, IsDisabled = false },   // read
            new() { Id = 4, UserId = 1, IsRead = false, IsDisabled = true },   // disabled
            new() { Id = 5, UserId = 2, IsRead = false, IsDisabled = false },  // other user
        };

        Mock<DbSet<Notification>> dbSet = DbSetMockHelper.CreateMockDbSet(notifications);
        var context = new Mock<INotificationDbContext>();
        context.Setup(c => c.Notifications).Returns(dbSet.Object);

        // Cache miss: invoke the supplied factory.
        var cache = new Mock<ICacheRepository>();
        cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<UnreadCountDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<UnreadCountDto>>, TimeSpan?, CancellationToken>(
                async (_, factory, _, ct) => await factory(ct));

        var handler = new GetUnreadCount.Handler(context.Object, cache.Object);

        Result<UnreadCountDto> result = await handler.Handle(
            new GetUnreadCount.Request { UserId = 1 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }
}
