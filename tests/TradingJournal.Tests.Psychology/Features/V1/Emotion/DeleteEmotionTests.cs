using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Emotion;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Emotion;

public class DeleteEmotionValidatorTests
{
    private static readonly DeleteEmotion.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var request = new DeleteEmotion.Request(0);

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Id_Is_Valid()
    {
        var request = new DeleteEmotion.Request(1);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }
}

public class DeleteEmotionHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private DeleteEmotion.Handler _handler = null!;

    public DeleteEmotionHandlerTests()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _cacheMock = new Mock<ICacheRepository>();
        _handler = new DeleteEmotion.Handler(_contextMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Emotion_Not_Found()
    {
        var dbSet = new List<EmotionTag>().BuildMockDbSet();
        _contextMock.Setup(x => x.EmotionTags).Returns(dbSet.Object);
        var request = new DeleteEmotion.Request(99);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Success_And_Removes_Emotion_When_Found()
    {
        var emotion = new EmotionTag { Id = 1, Name = "Happy" };
        var dbSet = new List<EmotionTag> { emotion }.BuildMockDbSet();
        _contextMock.Setup(x => x.EmotionTags).Returns(dbSet.Object);
        var request = new DeleteEmotion.Request(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _contextMock.Verify(x => x.EmotionTags.Remove(emotion), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
