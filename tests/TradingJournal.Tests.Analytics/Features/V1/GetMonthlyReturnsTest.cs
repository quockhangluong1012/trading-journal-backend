using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Analytics.Features.V1;

public class GetMonthlyReturnsValidatorTests
{
    private static readonly GetMonthlyReturns.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetMonthlyReturns.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetMonthlyReturns.Request(AnalyticsFilter.OneMonth);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }
}

public class GetMonthlyReturnsHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetMonthlyReturns.Handler _handler = null!;
    private const int UserId = 1;

    public GetMonthlyReturnsHandlerTests()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetMonthlyReturns.Handler(_tradeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Monthly_Pnl_When_Closed_Trades_Exist()
    {
        // Arrange
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 50m, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 15), ClosedDate = new DateTime(2026, 1, 16), CreatedBy = UserId },
            new() { Id = 2, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = -20m, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 20), ClosedDate = new DateTime(2026, 2, 5), CreatedBy = UserId },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        var request = new GetMonthlyReturns.Request(AnalyticsFilter.AllTime, UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task Handle_Returns_Empty_When_No_Closed_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetMonthlyReturns.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
