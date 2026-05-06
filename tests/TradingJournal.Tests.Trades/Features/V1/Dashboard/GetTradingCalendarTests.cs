using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetTradingCalendarHandlerTests
{
    private Mock<ITradeProvider> _tradeProvider = null!;
    private GetTradingCalendar.Handler _handler = null!;
    public GetTradingCalendarHandlerTests() { _tradeProvider = new Mock<ITradeProvider>(); _handler = new GetTradingCalendar.Handler(_tradeProvider.Object); }

    [Fact] public async Task Handle_ReturnsCalendarResponse()
    {
        _tradeProvider.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());

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
