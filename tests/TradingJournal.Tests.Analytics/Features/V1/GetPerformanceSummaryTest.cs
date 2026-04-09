using FluentValidation;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Analytics.Features.V1;

public class GetPerformanceSummaryValidatorTests
{
    private static readonly GetPerformanceSummary.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetPerformanceSummary.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetPerformanceSummary.Request(AnalyticsFilter.OneMonth);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }
}

public class GetPerformanceSummaryHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetPerformanceSummary.Handler _handler = null!;
    private const int UserId = 1;

    public GetPerformanceSummaryHandlerTests()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetPerformanceSummary.Handler(_tradeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Valid_When_Trades_Exists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, ExitPrice = 1.105, Pnl = 50, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = now.AddDays(-2), ClosedDate = now.AddDays(-1), CreatedBy = UserId },
            new() { Id = 2, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, ExitPrice = 1.096, Pnl = -40, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = now.AddDays(-3), ClosedDate = now.AddDays(-2), CreatedBy = UserId },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var request = new GetPerformanceSummary.Request(AnalyticsFilter.AllTime, UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalClosed);
        Assert.Equal(10, result.Value.TotalPnl);
        Assert.Equal(1, result.Value.Wins);
        Assert.Equal(1, result.Value.Losses);
    }

    [Fact]
    public async Task Handle_Returns_Zero_Values_When_No_Closed_Trades()
    {
        // Arrange
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());

        var request = new GetPerformanceSummary.Request(AnalyticsFilter.AllTime, UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalPnl);
        Assert.Equal(0, result.Value.TotalClosed);
    }

    [Fact]
    public async Task Handle_Filters_By_User_Id()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, ExitPrice = 1.105, Pnl = 50, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = now.AddDays(-1), ClosedDate = now, CreatedBy = UserId },
            new() { Id = 2, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, ExitPrice = 1.105, Pnl = 999, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = now.AddDays(-1), ClosedDate = now, CreatedBy = 999 },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var request = new GetPerformanceSummary.Request(AnalyticsFilter.AllTime, UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.TotalPnl); // only user 1's trades
    }
}
