using FluentAssertions;
using Moq;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

[TestFixture]
public class GetEmotionAndWinRateHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private Mock<IEmotionTagProvider> _emotionTagProviderMock = null!;
    private GetEmotionAndWinRate.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _emotionTagProviderMock = new Mock<IEmotionTagProvider>();
        _handler = new GetEmotionAndWinRate.Handler(_tradeProviderMock.Object, _emotionTagProviderMock.Object);
    }

    [Test]
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

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var confident = result.Value.First(x => x.Id == 1);
        confident.WinRate.Should().Be(50);
        confident.Total.Should().Be(2);
        confident.Name.Should().Be("Confident");

        var nervous = result.Value.First(x => x.Id == 2);
        nervous.WinRate.Should().Be(100);
        nervous.Total.Should().Be(1);
    }

    [Test]
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

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Returns_Empty_List_When_No_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var request = new GetEmotionAndWinRate.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
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

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var item = result.Value[0];
        item.Total.Should().Be(1);
    }

    [Test]
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

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].WinRate.Should().Be(50);
    }
}

