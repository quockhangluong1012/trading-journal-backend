using Moq;
using MockQueryable.Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Emotion;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Emotion;

[TestFixture]
public class CreateEmotionValidatorTests
{
    private static readonly CreateEmotion.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var request = new CreateEmotion.Request("", EmotionType.Positive);
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(x => x.PropertyName == "Name"), Is.True);
    }

    [Test]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new CreateEmotion.Request("Happy", EmotionType.Positive);
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.True);
    }
}

[TestFixture]
public class CreateEmotionHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private CreateEmotion.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _cacheMock = new Mock<ICacheRepository>();
        _handler = new CreateEmotion.Handler(_contextMock.Object, _cacheMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Failure_When_Emotion_Name_Already_Exists()
    {
        var existing = new EmotionTag { Id = 1, Name = "Happy", EmotionType = EmotionType.Positive };
        _contextMock.Setup(x => x.EmotionTags).Returns(new[] { existing }.BuildMockDbSet<EmotionTag>().Object);
        var request = new CreateEmotion.Request("Happy", EmotionType.Positive);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public async Task Handle_Returns_Success_When_Emotion_Is_New()
    {
        _contextMock.Setup(x => x.EmotionTags).Returns(new List<EmotionTag>().BuildMockDbSet<EmotionTag>().Object);
        var request = new CreateEmotion.Request("NewEmotion", EmotionType.Positive);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        _contextMock.Verify(x => x.EmotionTags.AddAsync(It.Is<EmotionTag>(e => e.Name == "NewEmotion"), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

