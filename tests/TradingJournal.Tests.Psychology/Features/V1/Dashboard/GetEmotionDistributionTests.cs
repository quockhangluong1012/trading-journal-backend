using FluentAssertions;
using Moq;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

[TestFixture]
public class GetEmotionDistributionHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private Mock<IEmotionTagProvider> _emotionTagProviderMock = null!;
    private GetEmotionDistribution.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _emotionTagProviderMock = new Mock<IEmotionTagProvider>();
        _handler = new GetEmotionDistribution.Handler(_tradeProviderMock.Object, _emotionTagProviderMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Positive_Negative_Neutral_Distribution()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 1, EmotionTags = new List<int> { 1 } },
            new() { CreatedBy = 1, EmotionTags = new List<int> { 1 } },
            new() { CreatedBy = 1, EmotionTags = new List<int> { 2 } },
            new() { CreatedBy = 1, EmotionTags = new List<int> { 3 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Confident", EmotionType = EmotionType.Positive },
            new() { Id = 2, Name = "Nervous", EmotionType = EmotionType.Negative },
            new() { Id = 3, Name = "Neutral", EmotionType = EmotionType.Neutral },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionDistribution.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        var positive = result.Value.First(x => x.Name == "Positive");
        positive.Value.Should().Be(2);
        positive.Fill.Should().Be("#22c55e");

        var negative = result.Value.First(x => x.Name == "Negative");
        negative.Value.Should().Be(1);
        negative.Fill.Should().Be("#ef4444");

        var neutral = result.Value.First(x => x.Name == "Neutral");
        neutral.Value.Should().Be(1);
        neutral.Fill.Should().Be("#3b82f6");
    }

    [Test]
    public async Task Handle_Returns_Empty_List_When_No_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var request = new GetEmotionDistribution.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Only_Includes_Categories_With_Trades()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 1, EmotionTags = new List<int> { 1 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Confident", EmotionType = EmotionType.Positive },
            new() { Id = 2, Name = "Nervous", EmotionType = EmotionType.Negative },
            new() { Id = 3, Name = "Neutral", EmotionType = EmotionType.Neutral },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionDistribution.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Positive");
        result.Value[0].Value.Should().Be(1);
    }

    [Test]
    public async Task Handle_Trades_Without_EmotionTags_Are_Ignored()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 1, EmotionTags = null },
            new() { CreatedBy = 1, EmotionTags = new List<int>() },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Confident", EmotionType = EmotionType.Positive },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionDistribution.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Ignores_Other_Users_Trades_When_No_Matches()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { CreatedBy = 99, EmotionTags = new List<int> { 1 } },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var tags = new List<EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Confident", EmotionType = EmotionType.Positive },
        };
        _emotionTagProviderMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tags);

        var request = new GetEmotionDistribution.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}

