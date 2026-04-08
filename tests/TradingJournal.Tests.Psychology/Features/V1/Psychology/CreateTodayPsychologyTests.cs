using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Psychology;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;

namespace TradingJournal.Tests.Psychology.Features.V1.Psychology;

[TestFixture]
public class CreateTodayPsychologyValidatorTests
{
    private static readonly CreateTodayPsychology.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_ConfidentLevel_Is_Invalid()
    {
        var request = new CreateTodayPsychology.Request(new DateTime(2024, 6, 1), "", [1], 0, OverallMood.Neutral, (ConfidentLevel)99);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfidentLevel);
    }

    [Test]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new CreateTodayPsychology.Request(new DateTime(2024, 6, 1), "Notes", [1], 0, OverallMood.Neutral, ConfidentLevel.Neutral);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

[TestFixture]
public class CreateTodayPsychologyHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private CreateTodayPsychology.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _handler = new CreateTodayPsychology.Handler(_contextMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Success_When_Creating_New_Journal()
    {
        DbSet<PsychologyJournal> mockSet = new Mock<DbSet<PsychologyJournal>>().Object;

        mockSet.AddRangeAsync(new[] {
            new PsychologyJournal { 
                Id = 1,
                Date = new DateTime(2024, 5, 31) 
            }
        }, CancellationToken.None);

        _contextMock.Setup(x => x.PsychologyJournals).Returns(mockSet);
        var request = new CreateTodayPsychology.Request(Date: new DateTime(2024, 6, 1), TodayTradingReview: "",
            EmotionTags: [1], OverallMood: OverallMood.Neutral, UserId: 0);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _contextMock.Verify(x => x.PsychologyJournals.AddAsync(It.IsAny<PsychologyJournal>(), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

