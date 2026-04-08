using MockQueryable.Moq;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Psychology.Domain;
using TradingJournal.Modules.Psychology.Features.V1.Psychology;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Psychology;

[TestFixture]
public class GetPsychologyJournalsHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private GetPsychologyJournals.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _cacheMock = new Mock<ICacheRepository>();
        _handler = new GetPsychologyJournals.Handler(_contextMock.Object, _cacheMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Empty_When_No_Journals()
    {
        _contextMock.Setup(x => x.PsychologyJournals).Returns(new System.Collections.Generic.List<PsychologyJournal>().BuildMockDbSet<PsychologyJournal>().Object);
        var request = new GetPsychologyJournals.Request { Page = 1, PageSize = 10 };

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalItems.Should().Be(0);
    }
}


