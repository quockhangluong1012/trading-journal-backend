using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetWinLossRatioHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetWinLossRatio.Handler _handler = null!;
    public GetWinLossRatioHandlerTests() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetWinLossRatio.Handler(_ctx.Object); }

    [Fact] public async Task Handle_NoTrades_ReturnsEmptyList()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetWinLossRatio.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact] public async Task Handle_HasTrades_ReturnsWinLossCounts()
    {
        var fromDate = DateTime.UtcNow.AddDays(-30);
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, ClosedDate = fromDate.AddDays(1) },
            new() { Id = 2, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, ClosedDate = fromDate.AddDays(2) }
        };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetWinLossRatio.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }
}
