using MockQueryable.Moq;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Psychology.Features.V1.Dashboard;
using TradingJournal.Modules.Psychology.Infrastructure.Persistance;
using TradingJournal.Modules.Psychology.Common.Enum;
using TradingJournal.Modules.Psychology.ViewModel;

using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Psychology.Features.V1.Dashboard;

[TestFixture]
public class GetMoodAndConfidenceTrendHandlerTests
{
    private Mock<IPsychologyDbContext> _contextMock = null!;
    private Mock<ICacheRepository> _cacheMock = null!;
    private GetMoodAndConfidenceTrend.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<IPsychologyDbContext>();
        _cacheMock = new Mock<ICacheRepository>();
        _handler = new GetMoodAndConfidenceTrend.Handler(_contextMock.Object, _cacheMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Empty_List_When_Cache_Returns_Null()
    {
        _cacheMock
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<MoodAndConfidenceTrendViewModel>>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<MoodAndConfidenceTrendViewModel>?)null);

        var request = new GetMoodAndConfidenceTrend.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Returns_Trend_When_Data_Exists()
    {
        var cachedResult = new List<MoodAndConfidenceTrendViewModel>
        {
            new() { Date = new DateTime(2026, 1, 1), Mood = (int)OverallMood.Good, Confidence = (int)TradingJournal.Modules.Psychology.Domain.ConfidentLevel.High },
            new() { Date = new DateTime(2026, 1, 2), Mood = (int)OverallMood.Neutral, Confidence = (int)TradingJournal.Modules.Psychology.Domain.ConfidentLevel.Neutral },
            new() { Date = new DateTime(2026, 1, 3), Mood = (int)OverallMood.Good, Confidence = (int)TradingJournal.Modules.Psychology.Domain.ConfidentLevel.VeryHigh },
        };

        _cacheMock
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<MoodAndConfidenceTrendViewModel>>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var request = new GetMoodAndConfidenceTrend.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Date.Should().Be(new DateTime(2026, 1, 1));
        result.Value[0].Mood.Should().Be((int)OverallMood.Good);
        result.Value[0].Confidence.Should().Be((int)TradingJournal.Modules.Psychology.Domain.ConfidentLevel.High);
    }

    [Test]
    public async Task Handle_Returns_Empty_When_No_Journals()
    {
        var emptyResult = new List<MoodAndConfidenceTrendViewModel>();
        _cacheMock.Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<MoodAndConfidenceTrendViewModel>>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var request = new GetMoodAndConfidenceTrend.Request(1);
        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}


