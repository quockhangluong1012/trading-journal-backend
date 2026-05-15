using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

using SharedEnums = TradingJournal.Shared.Common.Enum;

public sealed class GetTradeAssetsHandlerTests
{
    private readonly Mock<ITradeDbContext> _contextMock;
    private readonly GetTradeAssets.Handler _handler;

    public GetTradeAssetsHandlerTests()
    {
        _contextMock = new Mock<ITradeDbContext>();
        _handler = new GetTradeAssets.Handler(_contextMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_DistinctNormalizedAssets_OrderedByMostRecentTrade()
    {
        DateTime now = new(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        List<TradeHistory> trades =
        [
            new() { Id = 1, CreatedBy = 7, Asset = " es ", Date = now.AddDays(-1), Position = SharedEnums.PositionType.Long, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
            new() { Id = 2, CreatedBy = 7, Asset = "NQ", Date = now, Position = SharedEnums.PositionType.Long, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
            new() { Id = 3, CreatedBy = 7, Asset = "nq", Date = now.AddDays(-3), Position = SharedEnums.PositionType.Short, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
            new() { Id = 4, CreatedBy = 7, Asset = "MES", Date = now.AddDays(-2), Position = SharedEnums.PositionType.Long, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
        ];

        _contextMock.Setup(context => context.TradeHistories)
            .Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeAssets.Request(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["NQ", "ES", "MES"], result.Value);
    }

    [Fact]
    public async Task Handle_Excludes_BlankAssets_And_OtherUsersTrades()
    {
        DateTime now = new(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        List<TradeHistory> trades =
        [
            new() { Id = 1, CreatedBy = 7, Asset = " ", Date = now, Position = SharedEnums.PositionType.Long, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
            new() { Id = 2, CreatedBy = 7, Asset = string.Empty, Date = now.AddDays(-1), Position = SharedEnums.PositionType.Long, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
            new() { Id = 3, CreatedBy = 7, Asset = "YM", Date = now.AddDays(-2), Position = SharedEnums.PositionType.Short, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
            new() { Id = 4, CreatedBy = 99, Asset = "RTY", Date = now.AddDays(-3), Position = SharedEnums.PositionType.Long, Status = SharedEnums.TradeStatus.Open, EntryPrice = 1, TargetTier1 = 2, StopLoss = 0.5m },
        ];

        _contextMock.Setup(context => context.TradeHistories)
            .Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeAssets.Request(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["YM"], result.Value);
    }

    [Fact]
    public async Task Handle_Returns_Empty_WhenUserHasNoPersistedAssets()
    {
        _contextMock.Setup(context => context.TradeHistories)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeAssets.Request(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_Limits_Result_ToMostRecentFiftyAssets()
    {
        DateTime now = new(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        List<TradeHistory> trades = Enumerable.Range(1, 55)
            .Select(index => new TradeHistory
            {
                Id = index,
                CreatedBy = 7,
                Asset = $"asset-{index}",
                Date = now.AddMinutes(-index),
                Position = SharedEnums.PositionType.Long,
                Status = SharedEnums.TradeStatus.Open,
                EntryPrice = 1,
                TargetTier1 = 2,
                StopLoss = 0.5m,
            })
            .ToList();

        _contextMock.Setup(context => context.TradeHistories)
            .Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeAssets.Request(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.Count);
        Assert.Equal("ASSET-1", result.Value.First());
        Assert.DoesNotContain("ASSET-55", result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_Returns_Failure_WhenUserContextIsInvalid(int invalidUserId)
    {
        _contextMock.Setup(context => context.TradeHistories)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradeAssets.Request(invalidUserId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Description.Contains("valid user context", StringComparison.OrdinalIgnoreCase));
    }
}