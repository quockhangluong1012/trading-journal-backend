using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class GetTradeDetailValidatorTests
{
    private readonly GetTradeDetail.Validator _validator = new();

    [Fact]
    public void Validate_Id_Zero_ReturnsInvalid()
    {
        var result = _validator.TestValidate(new GetTradeDetail.Request(0, 1));
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var result = _validator.TestValidate(new GetTradeDetail.Request(1, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class GetTradeDetailHandlerTests
{
    private readonly Mock<ITradeDbContext> _contextMock;
    private readonly GetTradeDetail.Handler _handler;

    public GetTradeDetailHandlerTests()
    {
        _contextMock = new Mock<ITradeDbContext>();
        _handler = new GetTradeDetail.Handler(_contextMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Success_For_Owning_User()
    {
        _contextMock.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>
        {
            new()
            {
                Id = 1,
                CreatedBy = 1,
                Asset = "EURUSD",
                Notes = "Test",
                IsRuleBroken = true,
                RuleBreakReason = "Risk critical: Max open positions reached.",
                AccountBalanceAtEntry = 12500m,
                RiskAmountAtEntry = 125m,
                SuggestedPositionUnits = 25000m,
                SuggestedPositionLots = 0.25m,
                RiskRewardRatioAtEntry = 2.5m,
                TradingSetupId = 55,
                TradeScreenShots = [],
                TradeEmotionTags = [],
                TradeChecklists = [],
                TradeTechnicalAnalysisTags = [],
            }
        }.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeDetail.Request(1, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EURUSD", result.Value.Asset);
        Assert.Equal(55, result.Value.TradingSetupId);
        Assert.True(result.Value.IsRuleBroken);
        Assert.Equal("Risk critical: Max open positions reached.", result.Value.RuleBreakReason);
        Assert.Equal(12500m, result.Value.AccountBalanceAtEntry);
        Assert.Equal(125m, result.Value.RiskAmountAtEntry);
        Assert.Equal(25000m, result.Value.SuggestedPositionUnits);
        Assert.Equal(0.25m, result.Value.SuggestedPositionLots);
        Assert.Equal(2.5m, result.Value.RiskRewardRatioAtEntry);
    }

    [Fact]
    public async Task Handle_Returns_Failure_For_Non_Owning_User()
    {
        _contextMock.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>
        {
            new()
            {
                Id = 1,
                CreatedBy = 7,
                Asset = "EURUSD",
                TradeScreenShots = [],
                TradeEmotionTags = [],
                TradeChecklists = [],
                TradeTechnicalAnalysisTags = [],
            }
        }.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeDetail.Request(1, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
