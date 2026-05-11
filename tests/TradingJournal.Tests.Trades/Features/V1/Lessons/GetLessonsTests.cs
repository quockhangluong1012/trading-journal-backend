using Moq;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.Lessons;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Lessons;

public sealed class GetLessonsHandlerTests
{
    private readonly Mock<ITradeDbContext> _context = new();

    private GetLessons.Handler CreateHandler() => new(_context.Object);

    [Fact]
    public async Task Handle_AppliesTagAndSearchFiltersAcrossKnowledgeContent()
    {
        List<LessonLearned> lessons =
        [
            new()
            {
                Id = 1,
                CreatedBy = 42,
                Title = "London AMD model",
                Content = "Wait for displacement after the liquidity sweep before executing.",
                Category = LessonCategory.EntryTiming,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Reviewing,
                ImpactScore = 8,
                CreatedDate = new DateTime(2026, 5, 6),
                Tags = ["AMD", "London open"]
            },
            new()
            {
                Id = 2,
                CreatedBy = 42,
                Title = "Risk cap",
                Content = "Protect capital first.",
                Category = LessonCategory.RiskManagement,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Reviewing,
                ImpactScore = 7,
                CreatedDate = new DateTime(2026, 5, 5),
                Tags = ["Risk"]
            },
            new()
            {
                Id = 3,
                CreatedBy = 7,
                Title = "Other user lesson",
                Content = "Wait for displacement after the liquidity sweep before executing.",
                Category = LessonCategory.EntryTiming,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Reviewing,
                ImpactScore = 9,
                CreatedDate = new DateTime(2026, 5, 4),
                Tags = ["AMD"]
            }
        ];

        _context.Setup(x => x.LessonsLearned)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessons.AsQueryable()).Object);
        _context.Setup(x => x.LessonTradeLinks)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<LessonTradeLink>().AsQueryable()).Object);

        var result = await CreateHandler().Handle(new GetLessons.Request
        {
            UserId = 42,
            Status = LessonStatus.Reviewing,
            SearchTerm = "displacement",
            Tags = ["AMD"],
            Page = 1,
            PageSize = 10,
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Values);
        var lesson = result.Value.Values.Single();
        Assert.Equal(1, lesson.Id);
        Assert.Equal(["AMD", "London open"], lesson.Tags);
    }

    [Fact]
    public async Task Handle_AppliesImpactLinkedTradeAndSortFilters()
    {
        List<LessonLearned> lessons =
        [
            new()
            {
                Id = 1,
                CreatedBy = 42,
                Title = "London AMD model",
                Content = "Wait for displacement after the liquidity sweep before executing.",
                Category = LessonCategory.EntryTiming,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Reviewing,
                ImpactScore = 8,
                CreatedDate = new DateTime(2026, 5, 6),
            },
            new()
            {
                Id = 2,
                CreatedBy = 42,
                Title = "Risk cap",
                Content = "Protect capital first.",
                Category = LessonCategory.RiskManagement,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Applied,
                ImpactScore = 10,
                CreatedDate = new DateTime(2026, 5, 5),
            },
            new()
            {
                Id = 3,
                CreatedBy = 42,
                Title = "Session prep checklist",
                Content = "Anchor bias to liquidity and session timing.",
                Category = LessonCategory.MarketBias,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Reviewing,
                ImpactScore = 9,
                CreatedDate = new DateTime(2026, 5, 4),
            }
        ];

        List<LessonTradeLink> lessonTradeLinks =
        [
            new() { Id = 1, LessonLearnedId = 1, TradeHistoryId = 101 },
            new() { Id = 2, LessonLearnedId = 3, TradeHistoryId = 201 },
            new() { Id = 3, LessonLearnedId = 3, TradeHistoryId = 202 },
            new() { Id = 4, LessonLearnedId = 3, TradeHistoryId = 203 },
        ];

        foreach (LessonLearned lesson in lessons)
        {
            lesson.LessonTradeLinks = [.. lessonTradeLinks.Where(link => link.LessonLearnedId == lesson.Id)];
        }

        _context.Setup(x => x.LessonsLearned)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessons.AsQueryable()).Object);
        _context.Setup(x => x.LessonTradeLinks)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessonTradeLinks.AsQueryable()).Object);

        var result = await CreateHandler().Handle(new GetLessons.Request
        {
            UserId = 42,
            MinimumImpactScore = 8,
            LinkedTradesOnly = true,
            SortBy = LessonSortOption.MostLinkedTrades,
            Page = 1,
            PageSize = 10,
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([3, 1], result.Value!.Values.Select(lesson => lesson.Id).ToArray());
        Assert.All(result.Value.Values, lesson => Assert.True(lesson.ImpactScore >= 8));
        Assert.All(result.Value.Values, lesson => Assert.True(lesson.LinkedTradesCount > 0));
    }

    [Fact]
    public async Task Handle_MostLinkedTradesSort_ExcludesDisabledLinks()
    {
        List<LessonLearned> lessons =
        [
            new()
            {
                Id = 1,
                CreatedBy = 42,
                Title = "Active links win",
                Content = "Keep only active evidence in the library sort.",
                Category = LessonCategory.Other,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Reviewing,
                ImpactScore = 7,
                CreatedDate = new DateTime(2026, 5, 6),
            },
            new()
            {
                Id = 2,
                CreatedBy = 42,
                Title = "Disabled links should not count",
                Content = "Disabled links should not inflate ranking.",
                Category = LessonCategory.Other,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Reviewing,
                ImpactScore = 7,
                CreatedDate = new DateTime(2026, 5, 5),
            }
        ];

        List<LessonTradeLink> lessonTradeLinks =
        [
            new() { Id = 1, LessonLearnedId = 1, TradeHistoryId = 101 },
            new() { Id = 2, LessonLearnedId = 1, TradeHistoryId = 102 },
            new() { Id = 3, LessonLearnedId = 2, TradeHistoryId = 201 },
            new() { Id = 4, LessonLearnedId = 2, TradeHistoryId = 202, IsDisabled = true },
            new() { Id = 5, LessonLearnedId = 2, TradeHistoryId = 203, IsDisabled = true },
        ];

        foreach (LessonLearned lesson in lessons)
        {
            lesson.LessonTradeLinks = [.. lessonTradeLinks.Where(link => link.LessonLearnedId == lesson.Id)];
        }

        _context.Setup(x => x.LessonsLearned)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessons.AsQueryable()).Object);
        _context.Setup(x => x.LessonTradeLinks)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessonTradeLinks.AsQueryable()).Object);

        var result = await CreateHandler().Handle(new GetLessons.Request
        {
            UserId = 42,
            SortBy = LessonSortOption.MostLinkedTrades,
            Page = 1,
            PageSize = 10,
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2], result.Value!.Values.Select(lesson => lesson.Id).ToArray());
    }
}