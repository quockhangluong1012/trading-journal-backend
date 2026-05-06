using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Dtos;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetTradeStatisticsHandlerTests
{
    private Mock<ITradeProvider> _tradeProvider = null!;
    private GetTradingStatistic.Handler _handler = null!;
    public GetTradeStatisticsHandlerTests() { _tradeProvider = new Mock<ITradeProvider>(); _handler = new GetTradingStatistic.Handler(_tradeProvider.Object); }

    [Fact] public async Task Handle_NoTrades_ReturnsEmptyStatistic()
    {
        _tradeProvider.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalTrades);
    }

    [Fact] public async Task Handle_HasTrades_CalculatesStatistics()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, Date = DateTime.UtcNow.AddDays(-1) },
            new() { Id = 2, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, Date = DateTime.UtcNow.AddDays(-2) },
            new() { Id = 3, Status = SharedEnums.TradeStatus.Open, Pnl = null, Date = DateTime.UtcNow }
        };
        _tradeProvider.Setup(x => x.GetTradesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TotalTrades);
        Assert.Equal(1, result.Value.OpenPositions);
    }
}
