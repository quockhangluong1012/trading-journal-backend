using TradingJournal.Tests.Trades.Helpers;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;
using SharedEnums = TradingJournal.Shared.Common.Enum;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

[TestFixture]
public sealed class GetTradeStatisticsHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetTradingStatistic.Handler _handler = null!;
    [SetUp] public void SetUp() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetTradingStatistic.Handler(_ctx.Object); }

    [Test] public async Task Handle_NoTrades_ReturnsEmptyStatistic()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.TotalTrades, Is.EqualTo(0));
    }

    [Test] public async Task Handle_HasTrades_CalculatesStatistics()
    {
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, Date = DateTime.UtcNow.AddDays(-1) },
            new() { Id = 2, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, Date = DateTime.UtcNow.AddDays(-2) },
            new() { Id = 3, CreatedBy = 1, Status = SharedEnums.TradeStatus.Open, Pnl = null, Date = DateTime.UtcNow }
        };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.TotalTrades, Is.EqualTo(3));
        Assert.That(result.Value.OpenPositions, Is.EqualTo(1));
    }
}
