using Moq;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Shared.Interfaces;


namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

public class GetPsychologyHeatmapHandlerTests
{
    private Mock<ITradeProvider> _contextMock = null!;
    private Mock<IEmotionTagProvider> _cacheMock = null!;
    private GetPsychologyHeatmap.Handler _handler = null!;
    public GetPsychologyHeatmapHandlerTests()
    {
        _contextMock = new Mock<ITradeProvider>();
        _cacheMock = new Mock<IEmotionTagProvider>();
        _handler = new GetPsychologyHeatmap.Handler(_contextMock.Object, _cacheMock.Object);
    }
    [Fact]
    public async Task Handle_Returns_Empty_Heatmap_When_No_Data()
    {
        _contextMock.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new System.Collections.Generic.List<TradingJournal.Shared.Dtos.TradeCacheDto>());
        _cacheMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.Collections.Generic.List<TradingJournal.Shared.Dtos.EmotionTagCacheDto>());
        var request = new GetPsychologyHeatmap.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}

