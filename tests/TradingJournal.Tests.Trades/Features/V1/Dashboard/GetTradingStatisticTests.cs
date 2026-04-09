using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;
using SharedEnums = TradingJournal.Shared.Common.Enum;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetTradeStatisticsHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetTradingStatistic.Handler _handler = null!;
    public GetTradeStatisticsHandlerTests() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetTradingStatistic.Handler(_ctx.Object); }

    [Fact] public async Task Handle_NoTrades_ReturnsEmptyStatistic()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalTrades);
    }

    [Fact] public async Task Handle_HasTrades_CalculatesStatistics()
    {
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, Date = DateTime.UtcNow.AddDays(-1) },
            new() { Id = 2, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, Date = DateTime.UtcNow.AddDays(-2) },
            new() { Id = 3, CreatedBy = 1, Status = SharedEnums.TradeStatus.Open, Pnl = null, Date = DateTime.UtcNow }
        };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TotalTrades);
        Assert.Equal(1, result.Value.OpenPositions);
    }
}
