using TradingJournal.Tests.RiskManagement.Helpers;

namespace TradingJournal.Tests.RiskManagement.Services;

public class RiskContextProviderTests
{
    private const int UserId = 7;
    private readonly Mock<ITradeProvider> _tradeProvider = new();

    private RiskContextProvider BuildProvider(RiskConfig? config, IEnumerable<TradeCacheDto>? trades = null)
    {
        List<RiskConfig> configs = config is null ? [] : [config];
        var context = new Mock<IRiskDbContext>();
        context.Setup(c => c.RiskConfigs).Returns(DbSetMockHelper.CreateMockDbSet(configs).Object);

        _tradeProvider
            .Setup(p => p.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((trades ?? []).ToList());

        return new RiskContextProvider(context.Object, _tradeProvider.Object);
    }

    private static RiskConfig Config(
        decimal daily = 2m, decimal weekly = 5m, int maxOpen = 5, decimal balance = 10000m) => new()
        {
            CreatedBy = UserId,
            DailyLossLimitPercent = daily,
            WeeklyDrawdownCapPercent = weekly,
            MaxOpenPositions = maxOpen,
            AccountBalance = balance,
        };

    private static TradeCacheDto ClosedToday(int id, decimal pnl) => new()
    {
        Id = id,
        Asset = "EURUSD",
        Status = TradeStatus.Closed,
        Pnl = pnl,
        ClosedDate = DateTime.UtcNow.Date,
        CreatedBy = UserId,
    };

    private static TradeCacheDto Open(int id) => new()
    {
        Id = id,
        Asset = "EURUSD",
        Status = TradeStatus.Open,
        CreatedBy = UserId,
    };

    [Fact]
    public async Task GetRiskContext_Returns_Defaults_With_No_Config_And_No_Trades()
    {
        RiskContextProvider provider = BuildProvider(config: null);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.Equal(10000m, result.AccountBalance);
        Assert.Equal(2.0m, result.DailyLossLimitPercent);
        Assert.Equal(5.0m, result.WeeklyDrawdownCapPercent);
        Assert.Equal(5, result.MaxOpenPositions);
        Assert.False(result.IsDailyLimitBreached);
        Assert.False(result.IsWeeklyCapBreached);
        Assert.Empty(result.Alerts);
    }

    [Fact]
    public async Task GetRiskContext_Flags_Daily_Loss_Limit_Breach()
    {
        // Daily max = 200; weekly high so only daily breaches.
        var trades = new[] { ClosedToday(1, -250m) };
        RiskContextProvider provider = BuildProvider(Config(daily: 2m, weekly: 100m), trades);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.True(result.IsDailyLimitBreached);
        Assert.Contains(result.Alerts, a => a.Severity == "critical" && a.Title == "Daily Loss Limit Breached");
    }

    [Fact]
    public async Task GetRiskContext_Warns_When_Approaching_Daily_Limit()
    {
        // 150 / 200 = 75% used.
        var trades = new[] { ClosedToday(1, -150m) };
        RiskContextProvider provider = BuildProvider(Config(daily: 2m, weekly: 100m), trades);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.False(result.IsDailyLimitBreached);
        Assert.Contains(result.Alerts, a => a.Severity == "warning" && a.Title == "Approaching Daily Loss Limit");
    }

    [Fact]
    public async Task GetRiskContext_Flags_Weekly_Drawdown_Cap_Breach()
    {
        // Weekly max = 500; daily high so only weekly breaches.
        var trades = new[] { ClosedToday(1, -600m) };
        RiskContextProvider provider = BuildProvider(Config(daily: 100m, weekly: 5m), trades);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.True(result.IsWeeklyCapBreached);
        Assert.Contains(result.Alerts, a => a.Severity == "critical" && a.Title == "Weekly Drawdown Cap Breached");
    }

    [Fact]
    public async Task GetRiskContext_Flags_Max_Open_Positions_Reached()
    {
        var trades = new[] { Open(1), Open(2) };
        RiskContextProvider provider = BuildProvider(Config(maxOpen: 2), trades);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.Equal(2, result.OpenPositionCount);
        Assert.Contains(result.Alerts, a => a.Severity == "warning" && a.Title == "Max Open Positions Reached");
    }

    [Fact]
    public async Task GetRiskContext_Flags_Losing_Streak()
    {
        // Three losing trades today, no wins (each small enough not to breach the daily limit).
        var trades = new[] { ClosedToday(1, -10m), ClosedToday(2, -10m), ClosedToday(3, -10m) };
        RiskContextProvider provider = BuildProvider(Config(), trades);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.Equal(3, result.TodayLosses);
        Assert.Equal(0, result.TodayWins);
        Assert.Contains(result.Alerts, a => a.Severity == "warning" && a.Title == "Losing Streak Detected");
    }

    [Fact]
    public async Task GetRiskContext_Computes_Daily_Pnl_And_Percentage()
    {
        var trades = new[] { ClosedToday(1, 100m) };
        RiskContextProvider provider = BuildProvider(Config(balance: 10000m), trades);

        RiskAdvisorContextDto result = await provider.GetRiskContextAsync(UserId);

        Assert.Equal(100m, result.DailyPnl);
        Assert.Equal(1m, result.DailyPnlPercent); // 100 / 10000 * 100
        Assert.Equal(1, result.TodayWins);
        Assert.Equal(1, result.TodayTradeCount);
        Assert.Empty(result.Alerts); // a single small win triggers nothing
    }
}
