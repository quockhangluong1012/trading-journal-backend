using TradingJournal.Tests.RiskManagement.Helpers;

namespace TradingJournal.Tests.RiskManagement.Features.V1;

public class UpsertRiskConfigValidatorTests
{
    private static readonly UpsertRiskConfig.Validator _validator = new();

    private static UpsertRiskConfig.Command Valid() => new(2m, 5m, 1m, 5, 3, 10000m, 1);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void DailyLossLimit_Out_Of_Range_Is_Invalid(decimal value)
    {
        TestValidationResult<UpsertRiskConfig.Command> result =
            _validator.TestValidate(Valid() with { DailyLossLimitPercent = value });
        result.ShouldHaveValidationErrorFor(x => x.DailyLossLimitPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void RiskPerTrade_Out_Of_Range_Is_Invalid(decimal value)
    {
        TestValidationResult<UpsertRiskConfig.Command> result =
            _validator.TestValidate(Valid() with { RiskPerTradePercent = value });
        result.ShouldHaveValidationErrorFor(x => x.RiskPerTradePercent);
    }

    [Fact]
    public void MaxOpenPositions_Above_Fifty_Is_Invalid()
    {
        TestValidationResult<UpsertRiskConfig.Command> result =
            _validator.TestValidate(Valid() with { MaxOpenPositions = 51 });
        result.ShouldHaveValidationErrorFor(x => x.MaxOpenPositions);
    }

    [Fact]
    public void AccountBalance_Must_Be_Positive()
    {
        TestValidationResult<UpsertRiskConfig.Command> result =
            _validator.TestValidate(Valid() with { AccountBalance = 0m });
        result.ShouldHaveValidationErrorFor(x => x.AccountBalance);
    }

    [Fact]
    public void Valid_Command_Has_No_Errors()
    {
        TestValidationResult<UpsertRiskConfig.Command> result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class UpsertRiskConfigHandlerTests
{
    private const int UserId = 11;

    private static (Mock<IRiskDbContext> context, Mock<DbSet<RiskConfig>> configs, Mock<ICacheRepository> cache) Arrange(
        List<RiskConfig> existing)
    {
        Mock<DbSet<RiskConfig>> configs = DbSetMockHelper.CreateMockDbSet(existing);
        var context = new Mock<IRiskDbContext>();
        context.Setup(c => c.RiskConfigs).Returns(configs.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cache = new Mock<ICacheRepository>();
        cache.Setup(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (context, configs, cache);
    }

    [Fact]
    public async Task Handle_Creates_New_Config_When_None_Exists()
    {
        (Mock<IRiskDbContext> context, Mock<DbSet<RiskConfig>> configs, Mock<ICacheRepository> cache) = Arrange([]);
        var handler = new UpsertRiskConfig.Handler(context.Object, cache.Object);

        Result result = await handler.Handle(new UpsertRiskConfig.Command(2m, 5m, 1m, 5, 3, 10000m, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        configs.Verify(d => d.Add(It.IsAny<RiskConfig>()), Times.Once);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(CacheKeys.RiskConfigForUser(UserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Updates_Existing_Config_Without_Adding()
    {
        var existing = new RiskConfig
        {
            CreatedBy = UserId,
            DailyLossLimitPercent = 1m,
            WeeklyDrawdownCapPercent = 2m,
            RiskPerTradePercent = 0.5m,
            MaxOpenPositions = 2,
            MaxCorrelatedPositions = 1,
            AccountBalance = 5000m,
        };
        (Mock<IRiskDbContext> context, Mock<DbSet<RiskConfig>> configs, Mock<ICacheRepository> cache) = Arrange([existing]);
        var handler = new UpsertRiskConfig.Handler(context.Object, cache.Object);

        Result result = await handler.Handle(new UpsertRiskConfig.Command(3m, 6m, 2m, 8, 4, 20000m, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3m, existing.DailyLossLimitPercent);
        Assert.Equal(6m, existing.WeeklyDrawdownCapPercent);
        Assert.Equal(2m, existing.RiskPerTradePercent);
        Assert.Equal(8, existing.MaxOpenPositions);
        Assert.Equal(4, existing.MaxCorrelatedPositions);
        Assert.Equal(20000m, existing.AccountBalance);
        configs.Verify(d => d.Add(It.IsAny<RiskConfig>()), Times.Never);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(CacheKeys.RiskConfigForUser(UserId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
