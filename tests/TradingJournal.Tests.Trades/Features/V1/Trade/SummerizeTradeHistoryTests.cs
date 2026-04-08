using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Modules.Trades.Dto;
using TradingJournal.Modules.Trades.Domain;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

[TestFixture]
public sealed class SummerizeTradeHistoryValidatorTests
{
    private SummerizeTradeHistory.Validator _validator = null!;
    [SetUp] public void SetUp() => _validator = new SummerizeTradeHistory.Validator();

    [Test] public void Validate_ValidTradeId_ReturnsValid()
    {
        var result = _validator.Validate(new SummerizeTradeHistory.Request(1));
        result.IsValid.Should().BeTrue();
    }
    [Test] public void Validate_TradeIdZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new SummerizeTradeHistory.Request(0));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("TradeId"));
    }
}

[TestFixture]
public sealed class SummerizeTradeHistoryHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IOpenRouterAIService> _ai = null!;
    private SummerizeTradeHistory.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _ctx = new Mock<ITradeDbContext>();
        _ai = new Mock<IOpenRouterAIService>();
        _handler = new SummerizeTradeHistory.Handler(_ctx.Object, _ai.Object);
    }
    [Test]
    public async Task Handle_AiReturnsNull_ReturnsFailure()
    {
        _ai.Setup(x => x.GenerateTradingOrderSummary(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradeAnalysisResultDto?)null);
        var result = await _handler.Handle(new SummerizeTradeHistory.Request(1), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
    }
    [Test]
    public async Task Handle_AiReturnsResult_CreatesSummaryAndReturnsSuccess()
    {
        var aiResult = new TradeAnalysisResultDto { ExecutiveSummary = "summary", TechnicalInsights = "insights", PsychologyAnalysis = "psych", CriticalMistakes = new CriticalMistakesDto { Technical = [], Psychological = [] } };
        _ai.Setup(x => x.GenerateTradingOrderSummary(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiResult);

        var summarySet = new Mock<DbSet<TradingSummary>>();
        _ctx.Setup(x => x.TradingSummaries).Returns(summarySet.Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var tradeSet = new Mock<DbSet<TradeHistory>>();
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradeHistory?)null);

        var result = await _handler.Handle(new SummerizeTradeHistory.Request(1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }
}
