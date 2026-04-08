using FluentAssertions;
using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Shared.Interfaces;
using MockQueryable.Moq;
using TradingJournal.Modules.Psychology.ViewModel;


namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

[TestFixture]
public class GetPsychologyHeatmapHandlerTests
{
    private Mock<ITradeProvider> _contextMock = null!;
    private Mock<IEmotionTagProvider> _cacheMock = null!;
    private GetPsychologyHeatmap.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<ITradeProvider>();
        _cacheMock = new Mock<IEmotionTagProvider>();
        _handler = new GetPsychologyHeatmap.Handler(_contextMock.Object, _cacheMock.Object);
    }
    [Test]
    public async Task Handle_Returns_Empty_Heatmap_When_No_Data()
    {
        _contextMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.Collections.Generic.List<TradingJournal.Shared.Dtos.TradeCacheDto>());
        _cacheMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.Collections.Generic.List<TradingJournal.Shared.Dtos.EmotionTagCacheDto>());
        var request = new GetPsychologyHeatmap.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
