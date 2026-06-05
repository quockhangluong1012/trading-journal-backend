using TradingJournal.Tests.RiskManagement.Helpers;

namespace TradingJournal.Tests.RiskManagement.Services;

public class TradeRiskAssessmentServiceTests
{
    private const int UserId = 42;
    private readonly Mock<ITradeProvider> _tradeProvider = new();

    private TradeRiskAssessmentService BuildService(RiskConfig? config, IEnumerable<TradeCacheDto>? trades = null)
    {
        List<RiskConfig> configs = config is null ? [] : [config];
        var context = new Mock<IRiskDbContext>();
        context.Setup(c => c.RiskConfigs).Returns(DbSetMockHelper.CreateMockDbSet(configs).Object);

        _tradeProvider
            .Setup(p => p.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((trades ?? []).ToList());

        return new TradeRiskAssessmentService(context.Object, _tradeProvider.Object);
    }

    private static RiskConfig Config(
        decimal daily = 2m, decimal weekly = 5m, decimal riskPerTrade = 1m,
        int maxOpen = 5, int maxCorrelated = 3, decimal balance = 10000m) => new()
        {
            CreatedBy = UserId,
            DailyLossLimitPercent = daily,
            WeeklyDrawdownCapPercent = weekly,
            RiskPerTradePercent = riskPerTrade,
            MaxOpenPositions = maxOpen,
            MaxCorrelatedPositions = maxCorrelated,
            AccountBalance = balance,
        };

    private static TradeCacheDto OpenTrade(int id, string asset) => new()
    {
        Id = id,
        Asset = asset,
        Status = TradeStatus.Open,
        CreatedBy = UserId,
    };

    private static TradeCacheDto ClosedToday(int id, string asset, decimal pnl) => new()
    {
        Id = id,
        Asset = asset,
        Status = TradeStatus.Closed,
        Pnl = pnl,
        ClosedDate = DateTime.UtcNow.Date,
        CreatedBy = UserId,
    };

    [Fact]
    public async Task Assess_Is_Compliant_With_No_Config_No_Trades_And_Good_Risk_Reward()
    {
        TradeRiskAssessmentService service = BuildService(config: null);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "EURUSD", entryPrice: 1.1000m, stopLossPrice: 1.0900m, targetPrice: 1.1300m, TradeStatus.Open);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Alerts);
        Assert.Equal(10000m, result.AccountBalance); // default balance applied
        Assert.Equal(3m, result.RiskRewardRatio);    // reward 0.03 / risk 0.01
    }

    [Fact]
    public async Task Assess_Flags_Critical_When_Projected_Open_Positions_Exceed_Max()
    {
        // Uncorrelated assets so only the open-position guardrail fires.
        var trades = new[] { OpenTrade(1, "AAA"), OpenTrade(2, "BBB") };
        TradeRiskAssessmentService service = BuildService(Config(maxOpen: 2, maxCorrelated: 10), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "CCC", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Open);

        Assert.False(result.IsCompliant);
        Assert.Equal(3, result.OpenPositionCount);
        Assert.Contains(result.Alerts, a => a.Severity == "critical" && a.Message.Contains("open positions"));
    }

    [Fact]
    public async Task Assess_Warns_But_Stays_Compliant_When_Open_Positions_Equal_Max()
    {
        var trades = new[] { OpenTrade(1, "AAA") };
        TradeRiskAssessmentService service = BuildService(Config(maxOpen: 2, maxCorrelated: 10), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "CCC", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Open);

        Assert.True(result.IsCompliant);
        Assert.Contains(result.Alerts, a => a.Severity == "warning" && a.Message.Contains("max open positions limit"));
    }

    [Fact]
    public async Task Assess_Flags_Critical_When_Correlated_Exposure_Exceeds_Max()
    {
        // EURUSD/GBPUSD correlate at 0.85 (>= 0.7 threshold).
        var trades = new[] { OpenTrade(1, "GBPUSD") };
        TradeRiskAssessmentService service = BuildService(Config(maxOpen: 10, maxCorrelated: 1), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "EURUSD", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Open);

        Assert.False(result.IsCompliant);
        Assert.Equal(2, result.CorrelatedOpenPositionCount);
        Assert.Contains(result.Alerts, a => a.Severity == "critical" && a.Message.Contains("correlated exposure"));
    }

    [Fact]
    public async Task Assess_Flags_Critical_When_Daily_Loss_Limit_Breached()
    {
        // Daily max = 2% of 10,000 = 200; weekly set high so only the daily guardrail fires.
        var trades = new[] { ClosedToday(1, "EURUSD", pnl: -250m) };
        TradeRiskAssessmentService service = BuildService(Config(daily: 2m, weekly: 100m), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "EURUSD", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Closed);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Alerts, a => a.Severity == "critical" && a.Message.Contains("Daily loss limit already breached"));
    }

    [Fact]
    public async Task Assess_Warns_When_Daily_Loss_Limit_Elevated_But_Not_Breached()
    {
        // 150 / 200 = 75% used — warning, not critical.
        var trades = new[] { ClosedToday(1, "EURUSD", pnl: -150m) };
        TradeRiskAssessmentService service = BuildService(Config(daily: 2m, weekly: 100m), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "EURUSD", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Closed);

        Assert.True(result.IsCompliant);
        Assert.Contains(result.Alerts, a => a.Severity == "warning" && a.Message.Contains("Daily loss limit usage"));
    }

    [Fact]
    public async Task Assess_Flags_Critical_When_Weekly_Drawdown_Cap_Breached()
    {
        // Weekly max = 5% of 10,000 = 500; daily set high so only the weekly guardrail fires.
        var trades = new[] { ClosedToday(1, "EURUSD", pnl: -600m) };
        TradeRiskAssessmentService service = BuildService(Config(daily: 100m, weekly: 5m), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "EURUSD", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Closed);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Alerts, a => a.Severity == "critical" && a.Message.Contains("Weekly drawdown cap already breached"));
    }

    [Fact]
    public async Task Assess_Warns_When_Risk_Reward_Below_One()
    {
        TradeRiskAssessmentService service = BuildService(Config(daily: 100m, weekly: 100m));

        // risk 0.10, reward 0.05 => R:R 0.5
        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "EURUSD", entryPrice: 1.1000m, stopLossPrice: 1.0000m, targetPrice: 1.1500m, TradeStatus.Closed);

        Assert.True(result.IsCompliant); // warning only
        Assert.Equal(0.5m, result.RiskRewardRatio);
        Assert.Contains(result.Alerts, a => a.Severity == "warning" && a.Message.Contains("risk-reward ratio"));
    }

    [Fact]
    public async Task Assess_Excludes_The_Trade_Being_Edited_From_Position_Counts()
    {
        // Without exclusion: 1 existing open + new open = 2 > max(1) => critical.
        // With existingTradeId excluded: 0 existing + new open = 1 == max(1) => warning only.
        var trades = new[] { OpenTrade(99, "AAA") };
        TradeRiskAssessmentService service = BuildService(Config(maxOpen: 1, maxCorrelated: 10), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "BBB", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Open, existingTradeId: 99);

        Assert.True(result.IsCompliant);
        Assert.Equal(1, result.OpenPositionCount);
    }

    [Fact]
    public async Task Assess_Does_Not_Apply_Position_Guardrails_When_Trade_Is_Not_Open()
    {
        // Plenty of open positions, but the assessed trade is Closed — position guardrails must not fire.
        var trades = new[] { OpenTrade(1, "AAA"), OpenTrade(2, "BBB"), OpenTrade(3, "CCC") };
        TradeRiskAssessmentService service = BuildService(Config(maxOpen: 1, maxCorrelated: 1, daily: 100m, weekly: 100m), trades);

        TradeRiskAssessmentDto result = await service.AssessAsync(
            UserId, "DDD", 1.1000m, 1.0900m, 1.1300m, TradeStatus.Closed);

        Assert.True(result.IsCompliant);
        Assert.DoesNotContain(result.Alerts, a => a.Message.Contains("open positions"));
    }
}
