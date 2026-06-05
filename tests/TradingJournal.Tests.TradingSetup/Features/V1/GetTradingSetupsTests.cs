using TradingJournal.Tests.Setups.Helpers;

namespace TradingJournal.Tests.Setups.Features.V1;

public class GetTradingSetupsHandlerTests
{
    private const int UserId = 5;

    private static (GetTradingSetups.Handler handler, Mock<ICacheRepository> cache) Arrange(List<TradingSetup> setups)
    {
        Mock<DbSet<TradingSetup>> set = DbSetMockHelper.CreateMockDbSet(setups);
        var context = new Mock<ISetupDbContext>();
        context.Setup(c => c.TradingSetups).Returns(set.Object);

        var cache = new Mock<ICacheRepository>();
        // Simulate a cache miss: invoke the supplied factory.
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TradingSetupViewModel>>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, Func<CancellationToken, Task<List<TradingSetupViewModel>>> factory, TimeSpan? _, CancellationToken ct)
                => await factory(ct));

        return (new GetTradingSetups.Handler(context.Object, cache.Object), cache);
    }

    private static SetupStep Step(string nodeType) => new() { NodeType = nodeType, Label = "x" };

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Invalid()
    {
        (GetTradingSetups.Handler handler, _) = Arrange([]);

        Result<IReadOnlyCollection<TradingSetupViewModel>> result =
            await handler.Handle(new GetTradingSetups.Request(0), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Filters_By_Owner_And_Not_Disabled()
    {
        var mine = new TradingSetup { Id = 1, CreatedBy = UserId, Name = "Mine", CreatedDate = DateTime.UtcNow };
        var other = new TradingSetup { Id = 2, CreatedBy = 999, Name = "Other", CreatedDate = DateTime.UtcNow };
        var disabled = new TradingSetup { Id = 3, CreatedBy = UserId, Name = "Disabled", IsDisabled = true, CreatedDate = DateTime.UtcNow };

        (GetTradingSetups.Handler handler, Mock<ICacheRepository> cache) = Arrange([mine, other, disabled]);

        Result<IReadOnlyCollection<TradingSetupViewModel>> result =
            await handler.Handle(new GetTradingSetups.Request(UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Mine", result.Value.First().Name);
        cache.Verify(c => c.GetOrCreateAsync(
            CacheKeys.SetupsForUser(UserId),
            It.IsAny<Func<CancellationToken, Task<List<TradingSetupViewModel>>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Counts_Only_Actionable_Steps()
    {
        var setup = new TradingSetup
        {
            Id = 1,
            CreatedBy = UserId,
            Name = "WithSteps",
            CreatedDate = DateTime.UtcNow,
            Steps = [Step("start"), Step("step"), Step("decision"), Step("end")],
        };
        (GetTradingSetups.Handler handler, _) = Arrange([setup]);

        Result<IReadOnlyCollection<TradingSetupViewModel>> result =
            await handler.Handle(new GetTradingSetups.Request(UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // "step" + "decision" are actionable; "start" and "end" are excluded.
        Assert.Equal(2, result.Value.First().StepCount);
    }

    [Fact]
    public async Task Handle_Orders_By_Most_Recently_Updated_First()
    {
        var older = new TradingSetup
        {
            Id = 1,
            CreatedBy = UserId,
            Name = "Older",
            CreatedDate = DateTime.UtcNow.AddDays(-2),
        };
        var newer = new TradingSetup
        {
            Id = 2,
            CreatedBy = UserId,
            Name = "Newer",
            CreatedDate = DateTime.UtcNow.AddDays(-2),
            UpdatedDate = DateTime.UtcNow,
        };
        (GetTradingSetups.Handler handler, _) = Arrange([older, newer]);

        Result<IReadOnlyCollection<TradingSetupViewModel>> result =
            await handler.Handle(new GetTradingSetups.Request(UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Newer", result.Value.First().Name);
    }
}
