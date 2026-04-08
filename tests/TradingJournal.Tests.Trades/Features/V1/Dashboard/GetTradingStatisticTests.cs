using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

[TestFixture]
public sealed class GetTradeStatisticsHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetTradingStatistic.Handler _handler = null!;
    [SetUp] public void SetUp() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetTradingStatistic.Handler(_ctx.Object); }

    [Test] public async Task Handle_NoTrades_ReturnsEmptyStatistic()
    {
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeHistory>());
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalTrades.Should().Be(0);
    }

    [Test] public async Task Handle_HasTrades_CalculatesStatistics()
    {
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, Date = DateTime.UtcNow.AddDays(-1) },
            new() { Id = 2, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, Date = DateTime.UtcNow.AddDays(-2) },
            new() { Id = 3, CreatedBy = 1, Status = SharedEnums.TradeStatus.Open, Pnl = null, Date = DateTime.UtcNow }
        };
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetTradingStatistic.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalTrades.Should().Be(3);
        result.Value.OpenPositions.Should().Be(1);
    }
}
