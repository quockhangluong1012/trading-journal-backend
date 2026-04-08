using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Dashboard;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

[TestFixture]
public sealed class GetProfitTrajectoryValidatorTests
{
    private GetProfitTrajectory.Validator _validator = null!;
    [SetUp] public void SetUp() => _validator = new GetProfitTrajectory.Validator();

    [Test] public void Validate_ValidFilter_ReturnsValid()
    {
        var result = _validator.Validate(new GetProfitTrajectory.Request(DashboardFilter.OneMonth));
        result.IsValid.Should().BeTrue();
    }
    [Test] public void Validate_InvalidFilter_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetProfitTrajectory.Request((DashboardFilter)99));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid filter"));
    }
}

[TestFixture]
public sealed class GetProfitTrajectoryHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private GetProfitTrajectory.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _ctx = new Mock<ITradeDbContext>();
        _handler = new GetProfitTrajectory.Handler(_ctx.Object);
    }
    [Test]
    public async Task Handle_NoTrades_ReturnsEmptyList()
    {
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeHistory>());
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetProfitTrajectory.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
    [Test]
    public async Task Handle_HasTrades_ReturnsTrajectoryData()
    {
        var closedDate = DateTime.UtcNow.AddDays(-1);
        var trades = new List<TradeHistory>
        {
            new() { Id = 1, CreatedBy = 1, Status = TradingJournal.Shared.Common.Enum.TradeStatus.Closed, Pnl = 100, ClosedDate = closedDate }
        };
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);

        var result = await _handler.Handle(new GetProfitTrajectory.Request(DashboardFilter.OneMonth, 1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
