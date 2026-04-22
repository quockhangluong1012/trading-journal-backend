using Moq;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

public class GetEmotionFrequencyHandlerTests
{
    private Mock<ITradeProvider> _contextMock = null!;
    private Mock<IEmotionTagProvider> _cacheMock = null!;
    private GetEmotionFrequency.Handler _handler = null!;
    public GetEmotionFrequencyHandlerTests()
    {
        _contextMock = new Mock<ITradeProvider>();
        _cacheMock = new Mock<IEmotionTagProvider>();
        _handler = new GetEmotionFrequency.Handler(_contextMock.Object, _cacheMock.Object);
    }
    [Fact]
    public async Task Handle_Returns_Success()
    {
        _contextMock.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new System.Collections.Generic.List<TradingJournal.Shared.Dtos.TradeCacheDto>());
        _cacheMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.Collections.Generic.List<TradingJournal.Shared.Dtos.EmotionTagCacheDto>());
        var result = await _handler.Handle(new GetEmotionFrequency.Request(1), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}

