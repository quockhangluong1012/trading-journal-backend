using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Analytics.Features.V1;

[TestFixture]
public class GetDayOfWeekBreakdownValidatorTests
{
    private static readonly GetDayOfWeekBreakdown.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetDayOfWeekBreakdown.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Test]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetDayOfWeekBreakdown.Request(AnalyticsFilter.SixMonths);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

[TestFixture]
public class GetDayOfWeekBreakdownHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetDayOfWeekBreakdown.Handler _handler = null!;
    private const int UserId = 1;

    [SetUp]
    public void SetUp()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetDayOfWeekBreakdown.Handler(_tradeProviderMock.Object);
    }

    [Test]
    public async Task Handle_Returns_All_Days_Of_Week()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 50m, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 12), ClosedDate = new DateTime(2026, 1, 12, 10, 0, 0), CreatedBy = UserId },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        var request = new GetDayOfWeekBreakdown.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7); // All 7 days returned
    }

    [Test]
    public async Task Handle_Returns_Zero_Values_When_No_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetDayOfWeekBreakdown.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);
        result.Value.Should().AllSatisfy(d => { d.Count.Should().Be(0); d.Pnl.Should().Be(0); });
    }
}
