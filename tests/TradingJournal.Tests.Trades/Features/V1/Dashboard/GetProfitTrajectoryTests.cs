using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class GetProfitTrajectoryValidatorTests
{
    private GetProfitTrajectory.Validator _validator = null!;
    public GetProfitTrajectoryValidatorTests() => _validator = new GetProfitTrajectory.Validator();

    [Fact] public void Validate_ValidFilter_ReturnsValid()
    {
        var result = _validator.Validate(new GetProfitTrajectory.Request(DashboardFilter.OneMonth));
        Assert.True(result.IsValid);
    }
    [Fact] public void Validate_InvalidFilter_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetProfitTrajectory.Request((DashboardFilter)99));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Invalid filter"));
    }
}

public sealed class GetProfitTrajectoryHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetProfitTrajectory.Handler _handler = null!;
    public GetProfitTrajectoryHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _handler = new GetProfitTrajectory.Handler(_ctx.Object);
    }
    [Fact]
    public async Task Handle_NoTrades_ReturnsEmptyList()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetProfitTrajectory.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
    [Fact]
    public async Task Handle_HasTrades_ReturnsTrajectoryData()
    {
        var closedDate = DateTime.UtcNow.AddDays(-1);
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = TradingJournal.Shared.Common.Enum.TradeStatus.Closed, Pnl = 100, ClosedDate = closedDate }
        };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetProfitTrajectory.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }
}
