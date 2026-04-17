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
                TradeScreenShots = [],
                TradeEmotionTags = [],
                TradeChecklists = [],
                TradeTechnicalAnalysisTags = [],
                TradingSummary = new TradingSummary
                {
                    Id = 1,
                    TradeId = 1,
                    ExecutiveSummary = "Summary",
                    TechnicalInsights = "Insights",
                    PsychologyAnalysis = "Psych",
                    CriticalMistakes = new CriticalMistakes()
                }
            }
        }.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeDetail.Request(1, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EURUSD", result.Value.Asset);
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
                TradingSummary = new TradingSummary
                {
                    Id = 1,
                    TradeId = 1,
                    ExecutiveSummary = "Summary",
                    TechnicalInsights = "Insights",
                    PsychologyAnalysis = "Psych",
                    CriticalMistakes = new CriticalMistakes()
                }
            }
        }.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeDetail.Request(1, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}