using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.Features.V1.Psychology;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;

namespace TradingJournal.Tests.Psychology.Features.V1.Psychology;

public class UpdatePsychologyJournalHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private UpdatePsychologyJournal.Handler _handler = null!;
    public UpdatePsychologyJournalHandlerTests()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _handler = new UpdatePsychologyJournal.Handler(_contextMock.Object);
    }
    [Fact]
    public async Task Handle_Returns_Failure_When_Journal_Not_Found()
    {
        var dbSet = new List<PsychologyJournal>().BuildMockDbSet();
        _contextMock.Setup(x => x.PsychologyJournals).Returns(dbSet.Object);
        var request = new UpdatePsychologyJournal.Request(99, DateTime.Now, "Notes", new List<int>(), 1, OverallMood.Neutral, ConfidentLevel.None);
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.True(result.IsFailure);
    }
    [Fact]
    public async Task Handle_Returns_Success_When_Found()
    {
        var journal = new PsychologyJournal { Id = 1, Date = DateTime.Now, CreatedBy = 1, PsychologyJournalEmotions = new List<PsychologyJournalEmotion>() };
        var dbSet = new List<PsychologyJournal> { journal }.BuildMockDbSet();
        _contextMock.Setup(x => x.PsychologyJournals).Returns(dbSet.Object);
        _contextMock.Setup(x => x.PsychologyJournalEmotions).Returns(new List<PsychologyJournalEmotion>().BuildMockDbSet<PsychologyJournalEmotion>().Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var request = new UpdatePsychologyJournal.Request(1, DateTime.Now, "Updated", new List<int> { 1 }, 1, OverallMood.Good, ConfidentLevel.High);
        var result = await _handler.Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

