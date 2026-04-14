using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetTradingCalendarHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetTradingCalendar.Handler _handler = null!;
    public GetTradingCalendarHandlerTests() { _ctx = new Mock<ITradeDbContext>(); _handler = new GetTradingCalendar.Handler(_ctx.Object); }

    [Fact] public async Task Handle_ReturnsCalendarResponse()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingCalendar.Request(1, 2024, null, DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
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
