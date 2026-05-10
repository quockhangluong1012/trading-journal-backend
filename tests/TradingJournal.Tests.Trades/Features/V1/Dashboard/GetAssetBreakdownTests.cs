using Moq;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetAssetBreakdownValidatorTests
{
    private readonly GetAssetBreakdown.Validator _validator = new();

    [Fact]
    public void Validate_ValidFilter_ReturnsValid()
    {
        var result = _validator.Validate(new GetAssetBreakdown.Request(DashboardFilter.OneMonth));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidFilter_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetAssetBreakdown.Request((DashboardFilter)99));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Invalid filter"));
    }
}

public sealed class GetAssetBreakdownHandlerTests
{
    private readonly Mock<ITradeProvider> _tradeProvider;
    private readonly GetAssetBreakdown.Handler _handler;

    public GetAssetBreakdownHandlerTests()
    {
        _tradeProvider = new Mock<ITradeProvider>();
        _handler = new GetAssetBreakdown.Handler(_tradeProvider.Object);
    }

    [Fact]
    public async Task Handle_GroupsClosedTradesByAssetAndOrdersByPnlContribution()
    {
        var recentClosedDate = DateTime.UtcNow.AddDays(-2);
        var oldClosedDate = DateTime.UtcNow.AddMonths(-6);
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "NQ", Status = TradeStatus.Closed, Pnl = 250m, ClosedDate = recentClosedDate },
            new() { Id = 2, Asset = "NQ", Status = TradeStatus.Closed, Pnl = -50m, ClosedDate = recentClosedDate },
            new() { Id = 3, Asset = "MNQ", Status = TradeStatus.Closed, Pnl = 100m, ClosedDate = recentClosedDate },
            new() { Id = 4, Asset = "ES", Status = TradeStatus.Open, Pnl = 900m, ClosedDate = recentClosedDate },
            new() { Id = 5, Asset = "RTY", Status = TradeStatus.Closed, Pnl = 1000m, ClosedDate = oldClosedDate },
        };

        _tradeProvider
            .Setup(provider => provider.GetTradesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        var result = await _handler.Handle(new GetAssetBreakdown.Request(DashboardFilter.OneMonth, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var orderedAssets = result.Value.ToArray();
        Assert.Equal("NQ", orderedAssets[0].Asset);
        Assert.Equal(200m, orderedAssets[0].Pnl);
        Assert.Equal(2, orderedAssets[0].Count);
        Assert.Equal(50m, orderedAssets[0].WinRate);

        Assert.Equal("MNQ", orderedAssets[1].Asset);
        Assert.Equal(100m, orderedAssets[1].Pnl);
        Assert.Equal(1, orderedAssets[1].Count);
        Assert.Equal(100m, orderedAssets[1].WinRate);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyListWhenNoClosedTradesMatchFilter()
    {
        _tradeProvider
            .Setup(provider => provider.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradeCacheDto>());

        var result = await _handler.Handle(new GetAssetBreakdown.Request(DashboardFilter.OneWeek, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_IgnoresTradesWithoutAssetsAndCalculatesZeroWinRate()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = string.Empty, Status = TradeStatus.Closed, Pnl = 250m, ClosedDate = DateTime.UtcNow.AddHours(-2) },
            new() { Id = 2, Asset = "MNQ", Status = TradeStatus.Closed, Pnl = -30m, ClosedDate = DateTime.UtcNow.AddHours(-2) },
            new() { Id = 3, Asset = "MNQ", Status = TradeStatus.Closed, Pnl = -20m, ClosedDate = DateTime.UtcNow.AddHours(-1) },
        };

        _tradeProvider
            .Setup(provider => provider.GetTradesAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        var result = await _handler.Handle(new GetAssetBreakdown.Request(DashboardFilter.AllTime, 9), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("MNQ", result.Value.First().Asset);
        Assert.Equal(-50m, result.Value.First().Pnl);
        Assert.Equal(2, result.Value.First().Count);
        Assert.Equal(0m, result.Value.First().WinRate);
    }
}