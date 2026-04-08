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
public sealed class GetWinLossRatioHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetWinLossRatio.Handler _handler = null!;
    [SetUp] public void SetUp() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetWinLossRatio.Handler(_ctx.Object); }

    [Test] public async Task Handle_NoTrades_ReturnsEmptyList()
    {
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeHistory>());
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetWinLossRatio.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test] public async Task Handle_HasTrades_ReturnsWinLossCounts()
    {
        var fromDate = DateTime.UtcNow.AddDays(-30);
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = 100, ClosedDate = fromDate.AddDays(1) },
            new() { Id = 2, CreatedBy = 1, Status = SharedEnums.TradeStatus.Closed, Pnl = -50, ClosedDate = fromDate.AddDays(2) }
        };
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetWinLossRatio.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }
}
