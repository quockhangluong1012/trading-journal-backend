using TradingJournal.Tests.Setups.Helpers;

namespace TradingJournal.Tests.Setups.Features.V1;

public class UpdatePlaybookRulesValidatorTests
{
    private static readonly UpdatePlaybookRules.Validator _validator = new();

    private static UpdatePlaybookRules.Request Valid() =>
        new(1, "entry", "exit", "trending", 2m, 3m, "15m,1H", "EUR/USD", 5);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void SetupId_Must_Be_Positive(int id)
    {
        TestValidationResult<UpdatePlaybookRules.Request> result = _validator.TestValidate(Valid() with { SetupId = id });
        result.ShouldHaveValidationErrorFor(x => x.SetupId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.01)]
    public void RiskPerTrade_Out_Of_Range_Is_Invalid(double value)
    {
        TestValidationResult<UpdatePlaybookRules.Request> result =
            _validator.TestValidate(Valid() with { RiskPerTrade = (decimal)value });
        result.ShouldHaveValidationErrorFor(x => x.RiskPerTrade);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(51)]
    public void TargetRiskReward_Out_Of_Range_Is_Invalid(double value)
    {
        TestValidationResult<UpdatePlaybookRules.Request> result =
            _validator.TestValidate(Valid() with { TargetRiskReward = (decimal)value });
        result.ShouldHaveValidationErrorFor(x => x.TargetRiskReward);
    }

    [Fact]
    public void Null_Optional_Numbers_Are_Valid()
    {
        TestValidationResult<UpdatePlaybookRules.Request> result =
            _validator.TestValidate(Valid() with { RiskPerTrade = null, TargetRiskReward = null });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_Request_Has_No_Errors()
    {
        TestValidationResult<UpdatePlaybookRules.Request> result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class UpdatePlaybookRulesHandlerTests
{
    private const int UserId = 5;

    private static (UpdatePlaybookRules.Handler handler, Mock<ISetupDbContext> context) Arrange(List<TradingSetup> setups)
    {
        Mock<DbSet<TradingSetup>> set = DbSetMockHelper.CreateMockDbSet(setups);
        var context = new Mock<ISetupDbContext>();
        context.Setup(c => c.TradingSetups).Returns(set.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (new UpdatePlaybookRules.Handler(context.Object), context);
    }

    private static UpdatePlaybookRules.Request Request(int setupId) =>
        new(setupId, " entry ", " exit ", " trending ", 1.5m, 2.5m, " 15m ", " EUR/USD ", UserId);

    [Fact]
    public async Task Handle_Returns_Failure_When_Not_Found()
    {
        (UpdatePlaybookRules.Handler handler, _) = Arrange([]);

        Result<bool> result = await handler.Handle(Request(99), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Updates_Fields_On_Success()
    {
        var setup = new TradingSetup { Id = 1, CreatedBy = UserId };
        (UpdatePlaybookRules.Handler handler, Mock<ISetupDbContext> context) = Arrange([setup]);

        Result<bool> result = await handler.Handle(Request(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("entry", setup.EntryRules);
        Assert.Equal("exit", setup.ExitRules);
        Assert.Equal("trending", setup.IdealMarketConditions);
        Assert.Equal(1.5m, setup.RiskPerTrade);
        Assert.Equal(2.5m, setup.TargetRiskReward);
        Assert.Equal("15m", setup.PreferredTimeframes);
        Assert.Equal("EUR/USD", setup.PreferredAssets);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Invalid()
    {
        (UpdatePlaybookRules.Handler handler, _) = Arrange([]);

        Result<bool> result = await handler.Handle(Request(1) with { UserId = 0 }, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
