using TradingJournal.Tests.Setups.Helpers;

namespace TradingJournal.Tests.Setups.Features.V1;

public class RetireSetupValidatorTests
{
    private static readonly RetireSetup.Validator _validator = new();

    private static RetireSetup.Request Valid() => new(1, "No longer profitable", 5);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetupId_Must_Be_Positive(int id)
    {
        TestValidationResult<RetireSetup.Request> result = _validator.TestValidate(Valid() with { SetupId = id });
        result.ShouldHaveValidationErrorFor(x => x.SetupId);
    }

    [Fact]
    public void Reason_Is_Required()
    {
        TestValidationResult<RetireSetup.Request> result = _validator.TestValidate(Valid() with { Reason = "" });
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Reason_Above_Max_Length_Is_Invalid()
    {
        TestValidationResult<RetireSetup.Request> result =
            _validator.TestValidate(Valid() with { Reason = new string('x', 1001) });
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Valid_Request_Has_No_Errors()
    {
        TestValidationResult<RetireSetup.Request> result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class RetireSetupHandlerTests
{
    private const int UserId = 5;

    private static (RetireSetup.Handler handler, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) Arrange(
        List<TradingSetup> setups)
    {
        Mock<DbSet<TradingSetup>> set = DbSetMockHelper.CreateMockDbSet(setups);
        var context = new Mock<ISetupDbContext>();
        context.Setup(c => c.TradingSetups).Returns(set.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cache = new Mock<ICacheRepository>();
        cache.Setup(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (new RetireSetup.Handler(context.Object, cache.Object), context, cache);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        (RetireSetup.Handler handler, _, _) = Arrange([]);

        Result<bool> result = await handler.Handle(new RetireSetup.Request(99, "reason", UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Already_Retired()
    {
        var setup = new TradingSetup { Id = 1, CreatedBy = UserId, Status = SetupStatus.Retired };
        (RetireSetup.Handler handler, _, _) = Arrange([setup]);

        Result<bool> result = await handler.Handle(new RetireSetup.Request(1, "reason", UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Retires_Setup_On_Success()
    {
        var setup = new TradingSetup { Id = 1, CreatedBy = UserId, Status = SetupStatus.Active };
        (RetireSetup.Handler handler, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) = Arrange([setup]);

        Result<bool> result = await handler.Handle(new RetireSetup.Request(1, "  done  ", UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SetupStatus.Retired, setup.Status);
        Assert.Equal("done", setup.RetiredReason);
        Assert.NotNull(setup.RetiredDate);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(CacheKeys.SetupsForUser(UserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Invalid()
    {
        (RetireSetup.Handler handler, _, _) = Arrange([]);

        Result<bool> result = await handler.Handle(new RetireSetup.Request(1, "reason", 0), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
