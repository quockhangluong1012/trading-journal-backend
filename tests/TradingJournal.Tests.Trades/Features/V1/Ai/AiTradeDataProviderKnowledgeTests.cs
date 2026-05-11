using Moq;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class AiTradeDataProviderKnowledgeTests
{
    private readonly Mock<ITradeDbContext> _context = new();
    private readonly Mock<IReviewSnapshotBuilder> _snapshotBuilder = new();
    private readonly Mock<IEmotionTagProvider> _emotionTagProvider = new();
    private readonly Mock<IPsychologyProvider> _psychologyProvider = new();
    private readonly Mock<ISetupProvider> _setupProvider = new();

    private AiTradeDataProvider CreateProvider() =>
        new(
            _context.Object,
            _snapshotBuilder.Object,
            _emotionTagProvider.Object,
            _psychologyProvider.Object,
            _setupProvider.Object);

    [Fact]
    public async Task GetResearchKnowledgeContextAsync_ReturnsRelevantLessonsPlaybooksAndDailyNotes()
    {
        List<LessonLearned> lessons =
        [
            new()
            {
                Id = 1,
                CreatedBy = 42,
                Title = "Risk rules",
                Content = "Protect capital first and stop after the daily drawdown limit.",
                Category = LessonCategory.RiskManagement,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Applied,
                ImpactScore = 10,
                CreatedDate = new DateTime(2026, 4, 1)
            },
            new()
            {
                Id = 2,
                CreatedBy = 42,
                Title = "AMD displacement checklist",
                Content = "Wait for the liquidity sweep, displacement, and retrace during the London open.",
                KeyTakeaway = "No execution before the displacement confirms intent.",
                ActionItems = "Review three London sessions on NQ.",
                Category = LessonCategory.EntryTiming,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Reviewing,
                ImpactScore = 9,
                CreatedDate = new DateTime(2026, 5, 4)
            }
        ];

        _context
            .Setup(x => x.LessonsLearned)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessons.AsQueryable()).Object);

        _setupProvider
            .Setup(provider => provider.GetPlaybookKnowledgeItemsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PlaybookKnowledgeItemDto
                {
                    SetupId = 91,
                    Name = "London AMD continuation",
                    Description = "Use AMD sequencing after the opening liquidity sweep.",
                    Status = "Active",
                    EntryRules = "Wait for displacement and retrace into imbalance.",
                    ExitRules = "Scale at opposing liquidity.",
                    IdealMarketConditions = "London open with a clear sweep.",
                    PreferredTimeframes = "5m,15m",
                    PreferredAssets = "NQ"
                },
                new PlaybookKnowledgeItemDto
                {
                    SetupId = 92,
                    Name = "Asia mean reversion",
                    Description = "Fade quiet range extremes.",
                    Status = "Draft"
                }
            ]);

        _psychologyProvider
            .Setup(provider => provider.GetDailyNoteKnowledgeItemsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DailyNoteKnowledgeItemDto
                {
                    DailyNoteId = 201,
                    NoteDate = new DateOnly(2026, 5, 6),
                    DailyBias = "Bullish",
                    MarketStructureNotes = "Expect AMD displacement after the London liquidity sweep.",
                    KeyLevelsAndLiquidity = "Prior day low sweep then delivery higher.",
                    SessionFocus = "London open",
                    RiskAppetite = "Normal",
                    MentalState = "Patient",
                    KeyRulesAndReminders = "Do not chase before displacement."
                },
                new DailyNoteKnowledgeItemDto
                {
                    DailyNoteId = 202,
                    NoteDate = new DateOnly(2026, 5, 5),
                    DailyBias = "Neutral",
                    MarketStructureNotes = "No clean theme.",
                    SessionFocus = "NY only",
                    RiskAppetite = "Conservative",
                    MentalState = "Flat"
                }
            ]);

        ResearchKnowledgeContextDto result = await CreateProvider().GetResearchKnowledgeContextAsync(
            42,
            "How does AMD displacement fit my London open prep?",
            CancellationToken.None);

        Assert.Equal("How does AMD displacement fit my London open prep?", result.FocusQuery);
        Assert.Equal(2, result.Lessons[0].LessonId);
        Assert.Equal(91, result.Playbooks[0].SetupId);
        Assert.Equal(201, result.DailyNotes[0].DailyNoteId);
        Assert.DoesNotContain(result.Playbooks, playbook => playbook.SetupId == 92);
    }

    [Fact]
    public async Task GetLessonKnowledgeContextAsync_ReturnsMatchingActiveLessonsOrderedByRelevance()
    {
        List<LessonLearned> lessons =
        [
            new()
            {
                Id = 1,
                CreatedBy = 42,
                Title = "Risk rules",
                Content = "Protect capital first and stop after the daily drawdown limit.",
                KeyTakeaway = "Preserve capital.",
                ActionItems = "Stop trading after -2R.",
                Category = LessonCategory.RiskManagement,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Applied,
                ImpactScore = 10,
                CreatedDate = new DateTime(2026, 4, 1),
                LessonTradeLinks = [new LessonTradeLink { TradeHistoryId = 701 }]
            },
            new()
            {
                Id = 2,
                CreatedBy = 42,
                Title = "AMD accumulation map",
                Content = "AMD starts with accumulation before the liquidity sweep and displacement.",
                KeyTakeaway = "Mark the initial balance before the sweep.",
                ActionItems = "Replay three London open NQ sessions.",
                Category = LessonCategory.MarketBias,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Applied,
                ImpactScore = 8,
                CreatedDate = new DateTime(2026, 5, 2),
                LessonTradeLinks =
                [
                    new LessonTradeLink { TradeHistoryId = 702 },
                    new LessonTradeLink { TradeHistoryId = 703 }
                ]
            },
            new()
            {
                Id = 3,
                CreatedBy = 42,
                Title = "Displacement checklist",
                Content = "Require displacement after the sweep to confirm intent.",
                KeyTakeaway = "No entry before displacement.",
                ActionItems = "Wait for a strong body close through the dealing range.",
                Category = LessonCategory.EntryTiming,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.New,
                ImpactScore = 9,
                CreatedDate = new DateTime(2026, 5, 4)
            },
            new()
            {
                Id = 4,
                CreatedBy = 42,
                Title = "Archived note",
                Content = "Old content.",
                Category = LessonCategory.Other,
                Severity = LessonSeverity.Minor,
                Status = LessonStatus.Archived,
                ImpactScore = 9,
                CreatedDate = new DateTime(2026, 5, 5)
            },
            new()
            {
                Id = 5,
                CreatedBy = 7,
                Title = "Other user lesson",
                Content = "Should never leak into another user's knowledge context.",
                Category = LessonCategory.Other,
                Severity = LessonSeverity.Minor,
                Status = LessonStatus.Applied,
                ImpactScore = 9,
                CreatedDate = new DateTime(2026, 5, 6)
            }
        ];

        _context
            .Setup(x => x.LessonsLearned)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessons.AsQueryable()).Object);

        var result = await CreateProvider().GetLessonKnowledgeContextAsync(
            42,
            "Teach me AMD displacement on NQ.",
            2,
            CancellationToken.None);

        Assert.Equal("Teach me AMD displacement on NQ.", result.FocusQuery);
        Assert.Equal([3, 2], result.Lessons.Select(lesson => lesson.LessonId));
        Assert.DoesNotContain(result.Lessons, lesson => lesson.Status == "Archived");
        Assert.Equal([702, 703], result.Lessons.Single(lesson => lesson.LessonId == 2).LinkedTradeIds);
    }

    [Fact]
    public async Task GetLessonKnowledgeContextAsync_WhenPromptIsGeneric_FallsBackToTopLessons()
    {
        List<LessonLearned> lessons =
        [
            new()
            {
                Id = 11,
                CreatedBy = 42,
                Title = "Risk rules",
                Content = "Protect capital first.",
                Category = LessonCategory.RiskManagement,
                Severity = LessonSeverity.Critical,
                Status = LessonStatus.Applied,
                ImpactScore = 10,
                CreatedDate = new DateTime(2026, 5, 5)
            },
            new()
            {
                Id = 12,
                CreatedBy = 42,
                Title = "Bias review",
                Content = "Map higher timeframe context before the session.",
                Category = LessonCategory.MarketBias,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.New,
                ImpactScore = 8,
                CreatedDate = new DateTime(2026, 5, 4)
            },
            new()
            {
                Id = 13,
                CreatedBy = 42,
                Title = "Entry patience",
                Content = "Wait for confirmation before execution.",
                Category = LessonCategory.EntryTiming,
                Severity = LessonSeverity.Moderate,
                Status = LessonStatus.Applied,
                ImpactScore = 6,
                CreatedDate = new DateTime(2026, 5, 3)
            }
        ];

        _context
            .Setup(x => x.LessonsLearned)
            .Returns(DbSetMockHelper.CreateMockDbSet(lessons.AsQueryable()).Object);

        var result = await CreateProvider().GetLessonKnowledgeContextAsync(
            42,
            "Use my saved lessons to build a focused review plan.",
            2,
            CancellationToken.None);

        Assert.Equal([11, 12], result.Lessons.Select(lesson => lesson.LessonId));
    }
}