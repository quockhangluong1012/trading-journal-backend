using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Psychology;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;

namespace TradingJournal.Tests.Psychology.Features.V1.Psychology;

public class DeletePsychologyJournalValidatorTests
{
    private static readonly DeletePsychologyJournal.Validator _validator = new();
    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new DeletePsychologyJournal.Request { Id = 1, UserId = 1 };
        var result = _validator.Validate(request);
        Assert.True(result.IsValid);
    }
}

public class DeletePsychologyJournalHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private DeletePsychologyJournal.Handler _handler = null!;
    public DeletePsychologyJournalHandlerTests()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _handler = new DeletePsychologyJournal.Handler(_contextMock.Object);
    }
    [Fact]
    public async Task Handle_Returns_Failure_When_Journal_Not_Found()
    {
        var dbSet = new List<PsychologyJournal>().BuildMockDbSet();
        _contextMock.Setup(x => x.PsychologyJournals).Returns(dbSet.Object);
        var result = await _handler.Handle(new DeletePsychologyJournal.Request { Id = 99, UserId = 1 }, CancellationToken.None);
        Assert.True(result.IsFailure);
    }
    [Fact]
    public async Task Handle_Returns_Success_And_Removes_When_Found()
    {
        var journal = new PsychologyJournal { Id = 1, Date = DateTime.Now, CreatedBy = 1 };
        var dbSet = new List<PsychologyJournal> { journal }.BuildMockDbSet();
        _contextMock.Setup(x => x.PsychologyJournals).Returns(dbSet.Object);
        var result = await _handler.Handle(new DeletePsychologyJournal.Request { Id = 1, UserId = 1 }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        _contextMock.Verify(x => x.PsychologyJournals.Remove(journal), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
