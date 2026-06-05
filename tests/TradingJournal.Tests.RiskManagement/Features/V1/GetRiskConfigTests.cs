using TradingJournal.Tests.RiskManagement.Helpers;

namespace TradingJournal.Tests.RiskManagement.Features.V1;

public class GetRiskConfigHandlerTests
{
    private const int UserId = 31;

    private static GetRiskConfig.Handler Arrange(List<RiskConfig> configs)
    {
        var context = new Mock<IRiskDbContext>();
        context.Setup(c => c.RiskConfigs).Returns(DbSetMockHelper.CreateMockDbSet(configs).Object);

        // Simulate a cache miss: invoke the factory so the DB-read path is exercised.
        var cache = new Mock<ICacheRepository>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GetRiskConfig.RiskConfigViewModel>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<GetRiskConfig.RiskConfigViewModel>> factory, TimeSpan? _, CancellationToken ct) => factory(ct)!);

        return new GetRiskConfig.Handler(context.Object, cache.Object);
    }

    [Fact]
    public async Task Handle_Returns_Stored_Config_Values()
    {
        var stored = new RiskConfig
        {
            CreatedBy = UserId,
            DailyLossLimitPercent = 3m,
            WeeklyDrawdownCapPercent = 7m,
            RiskPerTradePercent = 1.5m,
            MaxOpenPositions = 8,
            MaxCorrelatedPositions = 4,
            AccountBalance = 25000m,
        };
        GetRiskConfig.Handler handler = Arrange([stored]);

        Result<GetRiskConfig.RiskConfigViewModel> result =
            await handler.Handle(new GetRiskConfig.Request(UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3m, result.Value.DailyLossLimitPercent);
        Assert.Equal(7m, result.Value.WeeklyDrawdownCapPercent);
        Assert.Equal(1.5m, result.Value.RiskPerTradePercent);
        Assert.Equal(8, result.Value.MaxOpenPositions);
        Assert.Equal(4, result.Value.MaxCorrelatedPositions);
        Assert.Equal(25000m, result.Value.AccountBalance);
    }

    [Fact]
    public async Task Handle_Returns_Defaults_When_No_Config_Exists()
    {
        GetRiskConfig.Handler handler = Arrange([]);

        Result<GetRiskConfig.RiskConfigViewModel> result =
            await handler.Handle(new GetRiskConfig.Request(UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2.0m, result.Value.DailyLossLimitPercent);
        Assert.Equal(5.0m, result.Value.WeeklyDrawdownCapPercent);
        Assert.Equal(1.0m, result.Value.RiskPerTradePercent);
        Assert.Equal(5, result.Value.MaxOpenPositions);
        Assert.Equal(3, result.Value.MaxCorrelatedPositions);
        Assert.Equal(10000m, result.Value.AccountBalance);
    }
}
