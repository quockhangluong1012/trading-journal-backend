using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Emotion;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Emotion;

public class GetEmotionsHandlerTests
{
    private Mock<IEmotionTagProvider> _providerMock = null!;
    private GetEmotions.Handler _handler = null!;

    public GetEmotionsHandlerTests()
    {
        _providerMock = new Mock<IEmotionTagProvider>();
        _handler = new GetEmotions.Handler(_providerMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Emotions_When_Data_Exists()
    {
        var emotions = new List<TradingJournal.Shared.Dtos.EmotionTagCacheDto>
        {
            new() { Id = 1, Name = "Happy" },
            new() { Id = 2, Name = "Sad" },
        };
        _providerMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(emotions);
        
        var request = new GetEmotions.Request();
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task Handle_Returns_NotFound_When_No_Data()
    {
        _providerMock.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradingJournal.Shared.Dtos.EmotionTagCacheDto>());
        
        var request = new GetEmotions.Request();
        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}

