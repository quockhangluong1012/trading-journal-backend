using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Emotion;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Emotion;

[TestFixture]
public class DeleteEmotionValidatorTests
{
    private static readonly DeleteEmotion.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var request = new DeleteEmotion.Request(1);

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Test]
    public void Should_Not_Have_Error_When_Id_Is_Valid()
    {
        var request = new DeleteEmotion.Request(1);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }
}

[TestFixture]
public class DeleteEmotionHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private DeleteEmotion.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _cacheMock = new Mock<ICacheRepository>();
        _handler = new DeleteEmotion.Handler(_contextMock.Object, _cacheMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Failure_When_Emotion_Not_Found()
    {
        _contextMock.Setup(x => x.EmotionTags.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmotionTag?)null);
        var request = new DeleteEmotion.Request(99);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Test]
    public async Task Handle_Returns_Success_And_Removes_Emotion_When_Found()
    {
        var emotion = new EmotionTag { Id = 1, Name = "Happy" };
        _contextMock.Setup(x => x.EmotionTags.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emotion);
        var request = new DeleteEmotion.Request(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _contextMock.Verify(x => x.EmotionTags.Remove(emotion), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
