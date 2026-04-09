using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Psychology;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;

namespace TradingJournal.Tests.Psychology.Features.V1.Psychology;

public class CreateTodayPsychologyValidatorTests
{
    private static readonly CreateTodayPsychology.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_ConfidentLevel_Is_Invalid()
    {
        var request = new CreateTodayPsychology.Request(new DateTime(2024, 6, 1), "", [1], 0, OverallMood.Neutral, (ConfidentLevel)99);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfidentLevel);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new CreateTodayPsychology.Request(new DateTime(2024, 6, 1), "Notes", [1], 0, OverallMood.Neutral, ConfidentLevel.Neutral);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CreateTodayPsychologyHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private CreateTodayPsychology.Handler _handler = null!;

    public CreateTodayPsychologyHandlerTests()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _handler = new CreateTodayPsychology.Handler(_contextMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Creating_New_Journal()
    {
        var existingJournal = new PsychologyJournal { Id = 1, Date = new DateTime(2024, 5, 31) };
        var mockSetMock = new List<PsychologyJournal> { existingJournal }.BuildMockDbSet();
        
        _contextMock.Setup(x => x.PsychologyJournals).Returns(mockSetMock.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new CreateTodayPsychology.Request(Date: new DateTime(2024, 6, 1), TodayTradingReview: "",
            EmotionTags: [1], OverallMood: OverallMood.Neutral, UserId: 1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _contextMock.Verify(x => x.PsychologyJournals.AddAsync(It.IsAny<PsychologyJournal>(), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

