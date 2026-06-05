using TradingJournal.Tests.Setups.Helpers;

namespace TradingJournal.Tests.Setups.Features.V1;

public class DeleteTradingSetupValidatorTests
{
    private static readonly DeleteTradingSetup.Validator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Id_Must_Be_Positive(int id)
    {
        TestValidationResult<DeleteTradingSetup.Request> result = _validator.TestValidate(new DeleteTradingSetup.Request(id));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Valid_Id_Has_No_Errors()
    {
        TestValidationResult<DeleteTradingSetup.Request> result = _validator.TestValidate(new DeleteTradingSetup.Request(1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class DeleteTradingSetupHandlerTests
{
    private const int UserId = 5;

    private static (DeleteTradingSetup.Handler handler, Mock<DbSet<TradingSetup>> set, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) Arrange(
        List<TradingSetup> setups)
    {
        Mock<DbSet<TradingSetup>> set = DbSetMockHelper.CreateMockDbSet(setups);
        var context = new Mock<ISetupDbContext>();
        context.Setup(c => c.TradingSetups).Returns(set.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cache = new Mock<ICacheRepository>();
        cache.Setup(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (new DeleteTradingSetup.Handler(context.Object, cache.Object), set, context, cache);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        (DeleteTradingSetup.Handler handler, _, _, _) = Arrange([]);

        Result<bool> result = await handler.Handle(new DeleteTradingSetup.Request(99, UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Removes_Setup_On_Success()
    {
        var setup = new TradingSetup { Id = 1, CreatedBy = UserId };
        (DeleteTradingSetup.Handler handler, Mock<DbSet<TradingSetup>> set, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) = Arrange([setup]);

        Result<bool> result = await handler.Handle(new DeleteTradingSetup.Request(1, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        set.Verify(s => s.Remove(setup), Times.Once);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(CacheKeys.SetupsForUser(UserId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
