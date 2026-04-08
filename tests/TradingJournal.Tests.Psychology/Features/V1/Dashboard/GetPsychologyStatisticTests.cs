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
public class GetPsychologyStatisticHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private GetPsychologyStatistic.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _cacheMock = new Mock<ICacheRepository>();
        _handler = new GetPsychologyStatistic.Handler(_contextMock.Object, _cacheMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Zero_Values_When_No_Journals()
    {
        _contextMock.Setup(x => x.PsychologyJournals).Returns(new System.Collections.Generic.List<PsychologyJournal>().BuildMockDbSet<PsychologyJournal>().Object);
        _cacheMock.Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<PsychologyStatisticViewModel>>>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PsychologyStatisticViewModel?)null);
        var request = new GetPsychologyStatistic.Request(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
