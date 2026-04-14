using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Modules.Trades.Dto;
using TradingJournal.Modules.Trades.Domain;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class SummerizeTradeHistoryValidatorTests
{
    private SummerizeTradeHistory.Validator _validator = null!;
    public SummerizeTradeHistoryValidatorTests() => _validator = new SummerizeTradeHistory.Validator();

    [Fact] public void Validate_ValidTradeId_ReturnsValid()
    {
        var result = _validator.Validate(new SummerizeTradeHistory.Request(1));
        Assert.True(result.IsValid);
    }
    [Fact] public void Validate_TradeIdZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new SummerizeTradeHistory.Request(0));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("TradeId"));
    }
}

public sealed class SummerizeTradeHistoryHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IOpenRouterAIService> _ai = null!;
    private SummerizeTradeHistory.Handler _handler = null!;
    public SummerizeTradeHistoryHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _ai = new Mock<IOpenRouterAIService>();
        _handler = new SummerizeTradeHistory.Handler(_ctx.Object, _ai.Object);
    }
    [Fact]
    public async Task Handle_AiReturnsNull_ReturnsFailure()
    {
        _ai.Setup(x => x.GenerateTradingOrderSummary(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradeAnalysisResultDto?)null);
        var result = await _handler.Handle(new SummerizeTradeHistory.Request(1), CancellationToken.None);
        Assert.False(result.IsSuccess);
    }
    [Fact]
    public async Task Handle_AiReturnsResult_CreatesSummaryAndReturnsSuccess()
    {
        var aiResult = new TradeAnalysisResultDto { ExecutiveSummary = "summary", TechnicalInsights = "insights", PsychologyAnalysis = "psych", CriticalMistakes = new CriticalMistakesDto { Technical = [], Psychological = [] } };
        _ai.Setup(x => x.GenerateTradingOrderSummary(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiResult);

        _ctx.Setup(x => x.TradingSummaries).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSummary>().AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new SummerizeTradeHistory.Request(1), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}
