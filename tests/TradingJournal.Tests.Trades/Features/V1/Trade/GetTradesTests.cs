using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Dtos;
using TradingJournal.Tests.Trades.Helpers;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class GetTradesValidatorTests
{
    private GetTrades.Validator _validator = null!;
    public GetTradesValidatorTests() => _validator = new GetTrades.Validator();
    [Fact] public void Validate_DefaultValues_ReturnsValid()
    {
        var result = _validator.Validate(new GetTrades.Request { UserId = 1 });
        Assert.True(result.IsValid);
    }
    [Fact] public void Validate_PageZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 0 });
        Assert.False(result.IsValid);
    }
    [Fact] public void Validate_PageSizeZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 1, PageSize = 0 });
        Assert.False(result.IsValid);
    }

    [Fact] public void Validate_MissingNotesPageSizeWithinExtendedLimit_ReturnsValid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 1, PageSize = 1000, MissingNotesOnly = true, UserId = 1 });
        Assert.True(result.IsValid);
    }
}

public sealed class GetTradesHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IEmotionTagProvider> _emoProvider = null!;
    private GetTrades.Handler _handler = null!;
    public GetTradesHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _emoProvider = new Mock<IEmotionTagProvider>();
        _handler = new GetTrades.Handler(_ctx.Object, _emoProvider.Object);
    }
    [Fact]
    public async Task Handle_EmptyResults_ReturnsSuccess()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1 }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Values);
    }
    [Fact]
    public async Task Handle_HasResults_ReturnsPaginatedData()
    {
        var trades = new List<TradeHistory> { new() { Id = 1, Asset = "EURUSD", Position = SharedEnums.PositionType.Long, EntryPrice = 1.08m, Date = DateTime.UtcNow, Status = SharedEnums.TradeStatus.Open, TargetTier1 = 1.09m, StopLoss = 1.07m, CreatedBy = 1 } };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1, Page = 1, PageSize = 10 }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Values);
        Assert.Equal(1, result.Value.TotalItems);
    }

    [Fact]
    public async Task Handle_Applies_CombinedFilters_And_UserIsolation()
    {
        DateTime now = DateTime.UtcNow;
        var trades = new List<TradeHistory>
        {
            new()
            {
                Id = 1,
                Asset = "EURUSD",
                Position = SharedEnums.PositionType.Long,
                EntryPrice = 1.08m,
                Date = now.AddDays(-2),
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 1.09m,
                StopLoss = 1.07m,
                CreatedBy = 1,
                Pnl = 120m,
                ClosedDate = now.AddDays(-1)
            },
            new()
            {
                Id = 2,
                Asset = "EURUSD",
                Position = SharedEnums.PositionType.Long,
                EntryPrice = 1.08m,
                Date = now.AddDays(-20),
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 1.09m,
                StopLoss = 1.07m,
                CreatedBy = 1,
                Pnl = 75m,
                ClosedDate = now.AddDays(-19)
            },
            new()
            {
                Id = 3,
                Asset = "GBPUSD",
                Position = SharedEnums.PositionType.Long,
                EntryPrice = 1.25m,
                Date = now.AddDays(-2),
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 1.26m,
                StopLoss = 1.24m,
                CreatedBy = 1,
                Pnl = 80m,
                ClosedDate = now.AddDays(-1)
            },
            new()
            {
                Id = 4,
                Asset = "EURUSD",
                Position = SharedEnums.PositionType.Long,
                EntryPrice = 1.08m,
                Date = now.AddDays(-2),
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 1.09m,
                StopLoss = 1.07m,
                CreatedBy = 2,
                Pnl = 150m,
                ClosedDate = now.AddDays(-1)
            }
        };

        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var result = await _handler.Handle(new GetTrades.Request
        {
            UserId = 1,
            Asset = "EUR",
            Status = SharedEnums.TradeStatus.Closed,
            FromDate = now.AddDays(-7),
            ToDate = now,
            Page = 1,
            PageSize = 10
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Values);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal(1, result.Value.Values.First().Id);
    }

    [Fact]
    public async Task Handle_MissingNotesOnly_ReturnsTradesWithoutMeaningfulNotes()
    {
        DateTime now = DateTime.UtcNow;
        var trades = new List<TradeHistory>
        {
            new()
            {
                Id = 1,
                Asset = "NQ",
                Position = SharedEnums.PositionType.Long,
                EntryPrice = 21240m,
                Date = now.AddDays(-1),
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 21280m,
                StopLoss = 21210m,
                CreatedBy = 1,
                Notes = string.Empty,
            },
            new()
            {
                Id = 2,
                Asset = "MNQ",
                Position = SharedEnums.PositionType.Short,
                EntryPrice = 21240m,
                Date = now,
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 21180m,
                StopLoss = 21270m,
                CreatedBy = 1,
                Notes = "<p><br></p>",
            },
            new()
            {
                Id = 3,
                Asset = "ES",
                Position = SharedEnums.PositionType.Long,
                EntryPrice = 5400m,
                Date = now,
                Status = SharedEnums.TradeStatus.Closed,
                TargetTier1 = 5430m,
                StopLoss = 5385m,
                CreatedBy = 1,
                Notes = "Waited for the opening range sweep.",
            }
        };

        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var result = await _handler.Handle(new GetTrades.Request
        {
            UserId = 1,
            MissingNotesOnly = true,
            Page = 1,
            PageSize = 1000,
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Values.Count);
        Assert.Equal(new[] { 2, 1 }, result.Value.Values.Select(x => x.Id));
        Assert.All(result.Value.Values, trade => Assert.NotNull(trade.Notes));
    }
}
