using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Dtos;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetWinLossRatioHandlerTests
{
    private Mock<ITradeProvider> _tradeProvider = null!;
    private GetWinLossRatio.Handler _handler = null!;
    public GetWinLossRatioHandlerTests() { _tradeProvider = new Mock<ITradeProvider>(); _handler = new GetWinLossRatio.Handler(_tradeProvider.Object); }

    [Fact] public async Task Handle_NoTrades_ReturnsEmptyList()
    {
        _tradeProvider.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());

        var result = await _handler.Handle(new GetWinLossRatio.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact] public async Task Handle_HasTrades_ReturnsWinLossCounts()
    {
        var fromDate = DateTime.UtcNow.AddDays(-30);
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, ClosedDate = fromDate.AddDays(1) },
            new() { Id = 2, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, ClosedDate = fromDate.AddDays(2) }
        };
        _tradeProvider.Setup(x => x.GetTradesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var result = await _handler.Handle(new GetWinLossRatio.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }
}
