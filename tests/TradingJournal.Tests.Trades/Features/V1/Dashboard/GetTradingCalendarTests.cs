using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Shared.Common;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

[TestFixture]
public sealed class GetTradingCalendarHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetTradingCalendar.Handler _handler = null!;
    [SetUp] public void SetUp() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetTradingCalendar.Handler(_ctx.Object); }

    [Test] public async Task Handle_ReturnsCalendarResponse()
    {
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.As<IAsyncEnumerable<TradeHistory>>().Setup(x => x.GetAsyncEnumerator(CancellationToken.None)).Returns(new TestAsyncEnumerator<TradeHistory>(new List<TradeHistory>().GetEnumerator()));
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.SumAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, double>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(0.0);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetTradingCalendar.Request(1, 2024, null, DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}

public class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _enumerator;
    public TestAsyncEnumerator(IEnumerator<T> enumerator) => _enumerator = enumerator;
    public T Current => _enumerator.Current;
    public ValueTask<bool> MoveNextAsync() => new(_enumerator.MoveNext());
    public ValueTask DisposeAsync() { _enumerator.Dispose(); return default; }
}
