using TradingJournal.Tests.Setups.Helpers;

namespace TradingJournal.Tests.Setups.Features.V1;

public class ReactivateSetupHandlerTests
{
    private const int UserId = 7;

    private static (ReactivateSetup.Handler handler, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) Arrange(
        List<TradingSetup> setups)
    {
        Mock<DbSet<TradingSetup>> set = DbSetMockHelper.CreateMockDbSet(setups);
        var context = new Mock<ISetupDbContext>();
        context.Setup(c => c.TradingSetups).Returns(set.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cache = new Mock<ICacheRepository>();
        cache.Setup(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (new ReactivateSetup.Handler(context.Object, cache.Object), context, cache);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        (ReactivateSetup.Handler handler, _, _) = Arrange([]);

        Result<bool> result = await handler.Handle(new ReactivateSetup.Request(99, UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Retired()
    {
        var setup = new TradingSetup { Id = 1, CreatedBy = UserId, Status = SetupStatus.Active };
        (ReactivateSetup.Handler handler, _, _) = Arrange([setup]);

        Result<bool> result = await handler.Handle(new ReactivateSetup.Request(1, UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Reactivates_Retired_Setup_On_Success()
    {
        var setup = new TradingSetup
        {
            Id = 1,
            CreatedBy = UserId,
            Status = SetupStatus.Retired,
            RetiredReason = "old reason",
            RetiredDate = DateTime.UtcNow,
        };
        (ReactivateSetup.Handler handler, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) = Arrange([setup]);

        Result<bool> result = await handler.Handle(new ReactivateSetup.Request(1, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SetupStatus.Active, setup.Status);
        Assert.Null(setup.RetiredReason);
        Assert.Null(setup.RetiredDate);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(CacheKeys.SetupsForUser(UserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Invalid()
    {
        (ReactivateSetup.Handler handler, _, _) = Arrange([]);

        Result<bool> result = await handler.Handle(new ReactivateSetup.Request(1, 0), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
