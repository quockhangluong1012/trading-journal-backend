using Moq;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

public class GetEmotionAndWinRateHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private Mock<IEmotionTagProvider> _emotionTagProviderMock = null!;
    private GetEmotionAndWinRate.Handler _handler = null!;

    public GetEmotionAndWinRateHandlerTests()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _emotionTagProviderMock = new Mock<IEmotionTagProvider>();
        _handler = new GetEmotionAndWinRate.Handler(_tradeProviderMock.Object, _emotionTagProviderMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Success_With_WinRate_Calculation()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 1, ClosedDate = DateTime.UtcNow, Pnl = 100m, EmotionTags = new List<int> { 1, 2 } },
            new() { CreatedBy = 1, ClosedDate = DateTime.UtcNow, Pnl = -50m, EmotionTags = new List<int> { 1 } },
            new() { CreatedBy = 2, ClosedDate = DateTime.UtcNow, Pnl = 200m, EmotionTags = new List<int> { 1 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Confident", EmotionType = EmotionType.Positive },
            new() { Id = 2, Name = "Nervous", EmotionType = EmotionType.Negative },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionAndWinRate.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var confident = result.Value.First(x => x.Id == 1);
        Assert.Equal(50, confident.WinRate);
        Assert.Equal(2, confident.Total);
        Assert.Equal("Confident", confident.Name);

        var nervous = result.Value.First(x => x.Id == 2);
        Assert.Equal(100, nervous.WinRate);
        Assert.Equal(1, nervous.Total);
    }

    [Fact]
    public async Task Handle_Returns_Empty_List_When_No_Closed_Trades()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 1, ClosedDate = null, Pnl = null, EmotionTags = new List<int> { 1 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Confident", EmotionType = EmotionType.Positive },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionAndWinRate.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_Returns_Empty_List_When_No_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var request = new GetEmotionAndWinRate.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_Excludes_Trades_Belonging_To_Other_Users()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 99, ClosedDate = DateTime.UtcNow, Pnl = 100m, EmotionTags = new List<int> { 1 } },
            new() { CreatedBy = 1, ClosedDate = DateTime.UtcNow, Pnl = 50m, EmotionTags = new List<int> { 1 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Calm", EmotionType = EmotionType.Positive },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionAndWinRate.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Count);
        var item = result.Value[0];
        Assert.Equal(1, item.Total);
    }

    [Fact]
    public async Task Handle_Treats_Zero_Pnl_As_Non_Win()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 1, ClosedDate = DateTime.UtcNow, Pnl = 0m, EmotionTags = new List<int> { 1 } },
            new() { CreatedBy = 1, ClosedDate = DateTime.UtcNow, Pnl = 50m, EmotionTags = new List<int> { 1 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Greedy", EmotionType = EmotionType.Negative },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionAndWinRate.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Count);
        Assert.Equal(50, result.Value[0].WinRate);
    }
}

