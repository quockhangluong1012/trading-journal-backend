using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.Lessons;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Lessons;

public sealed class CreateLessonHandlerTests
{
    private readonly Mock<ITradeDbContext> _contextMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();

    private CreateLesson.Handler CreateHandler() =>
        new(_contextMock.Object, _httpContextAccessorMock.Object);

    private static CreateLesson.Request CreateRequest(List<int>? linkedTradeIds = null, List<string>? tags = null) =>
        new(
            Title: "Wait for confirmation",
            Content: "Entering before confirmation increased risk.",
            Category: LessonCategory.EntryTiming,
            Severity: LessonSeverity.Moderate,
            KeyTakeaway: "Wait for candle close.",
            ActionItems: "Review the entry checklist before submitting.",
            ImpactScore: 7,
            LinkedTradeIds: linkedTradeIds,
            Tags: tags);

    private void SetupUserId(int userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupTransactionalExecution()
    {
        _contextMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result<int>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Result<int>>> operation, CancellationToken ct) => operation(ct));
    }

    [Fact]
    public async Task Handle_WithLinkedTrades_CreatesLessonAndReturnsSuccess()
    {
        SetupUserId(42);

        var lessonSetMock = DbSetMockHelper.CreateMockDbSet(new List<LessonLearned>().AsQueryable());
        var linkSetMock = DbSetMockHelper.CreateMockDbSet(new List<LessonTradeLink>().AsQueryable());
        var tradeHistorySetMock = DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>
        {
            new() { Id = 11, CreatedBy = 42, Asset = "EURUSD", Notes = "A", EntryPrice = 1.1m, TargetTier1 = 1.2m, StopLoss = 1.0m },
            new() { Id = 12, CreatedBy = 42, Asset = "GBPUSD", Notes = "B", EntryPrice = 1.2m, TargetTier1 = 1.3m, StopLoss = 1.1m },
        }.AsQueryable());

        LessonLearned? createdLesson = null;
        List<LessonTradeLink>? createdLinks = null;

        lessonSetMock.Setup(x => x.AddAsync(It.IsAny<LessonLearned>(), It.IsAny<CancellationToken>()))
            .Callback<LessonLearned, CancellationToken>((lesson, _) => createdLesson = lesson)
            .ReturnsAsync((LessonLearned lesson, CancellationToken _) => null!);

        linkSetMock.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<LessonTradeLink>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LessonTradeLink>, CancellationToken>((links, _) => createdLinks = links.ToList())
            .Returns(Task.CompletedTask);

        _contextMock.Setup(x => x.LessonsLearned).Returns(lessonSetMock.Object);
        _contextMock.Setup(x => x.LessonTradeLinks).Returns(linkSetMock.Object);
        _contextMock.Setup(x => x.TradeHistories).Returns(tradeHistorySetMock.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        SetupTransactionalExecution();

        var result = await CreateHandler().Handle(CreateRequest([11, 11, 12]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(createdLesson);
        Assert.Equal("Wait for confirmation", createdLesson!.Title);
        Assert.NotNull(createdLinks);
        Assert.Equal(2, createdLinks!.Count);
        Assert.Contains(createdLinks, link => link.TradeHistoryId == 11);
        Assert.Contains(createdLinks, link => link.TradeHistoryId == 12);
        _contextMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<Result<int>>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithTags_PersistsDistinctNormalizedTags()
    {
        SetupUserId(42);

        var lessonSetMock = DbSetMockHelper.CreateMockDbSet(new List<LessonLearned>().AsQueryable());
        var linkSetMock = DbSetMockHelper.CreateMockDbSet(new List<LessonTradeLink>().AsQueryable());
        var tradeHistorySetMock = DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable());

        LessonLearned? createdLesson = null;

        lessonSetMock.Setup(x => x.AddAsync(It.IsAny<LessonLearned>(), It.IsAny<CancellationToken>()))
            .Callback<LessonLearned, CancellationToken>((lesson, _) => createdLesson = lesson)
            .ReturnsAsync((LessonLearned lesson, CancellationToken _) => null!);

        _contextMock.Setup(x => x.LessonsLearned).Returns(lessonSetMock.Object);
        _contextMock.Setup(x => x.LessonTradeLinks).Returns(linkSetMock.Object);
        _contextMock.Setup(x => x.TradeHistories).Returns(tradeHistorySetMock.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        SetupTransactionalExecution();

        var result = await CreateHandler().Handle(
            CreateRequest(tags: [" AMD ", "London open", "amd", "  "]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(createdLesson);
        Assert.Equal(["AMD", "London open"], createdLesson!.Tags);
        Assert.Equal("|AMD|London open|", createdLesson.TagsText);
    }

    [Fact]
    public async Task Handle_LinkedTradeFromAnotherUser_ReturnsFailureWithoutStartingTransaction()
    {
        SetupUserId(42);

        var tradeHistorySetMock = DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>
        {
            new() { Id = 11, CreatedBy = 7, Asset = "EURUSD", Notes = "A", EntryPrice = 1.1m, TargetTier1 = 1.2m, StopLoss = 1.0m },
        }.AsQueryable());

        _contextMock.Setup(x => x.TradeHistories).Returns(tradeHistorySetMock.Object);

        var result = await CreateHandler().Handle(CreateRequest([11]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        _contextMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<Result<int>>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SaveChangesThrows_ReturnsFailure()
    {
        SetupUserId(42);

        _contextMock.Setup(x => x.LessonsLearned).Returns(DbSetMockHelper.CreateMockDbSet(new List<LessonLearned>().AsQueryable()).Object);
        _contextMock.Setup(x => x.LessonTradeLinks).Returns(DbSetMockHelper.CreateMockDbSet(new List<LessonTradeLink>().AsQueryable()).Object);
        _contextMock.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        SetupTransactionalExecution();

        var result = await CreateHandler().Handle(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Description.Contains("DB error"));
    }
}