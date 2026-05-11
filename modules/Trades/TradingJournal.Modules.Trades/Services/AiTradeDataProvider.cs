using TradingJournal.Shared.Common;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Modules.Trades.Infrastructure;
using System.Globalization;

namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Implements IAiTradeDataProvider for cross-module access.
/// AiInsights module resolves this via DI to read trade data without
/// directly referencing TradeDbContext or Trades domain entities.
/// </summary>
internal sealed class AiTradeDataProvider(
    ITradeDbContext context,
    IReviewSnapshotBuilder snapshotBuilder,
    IEmotionTagProvider emotionTagProvider,
    IPsychologyProvider psychologyProvider,
    ISetupProvider setupProvider) : IAiTradeDataProvider
{
    private const int MaxKnowledgeCandidateLessons = 100;
    private const int MaxResearchPlaybooks = 4;
    private const int MaxResearchDailyNotes = 5;

    private static readonly HashSet<string> KnowledgeStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "are",
        "about",
        "break",
        "can",
        "down",
        "for",
        "from",
        "how",
        "into",
        "learn",
        "me",
        "on",
        "the",
        "this",
        "teach",
        "theme",
        "themes",
        "to",
        "turn",
        "use",
        "what",
        "with",
        "you",
        "saved",
        "lesson",
        "lessons",
        "identify",
        "main",
        "concept",
        "concepts",
        "execution",
        "study",
        "studying",
        "review",
        "plan",
        "week",
        "day",
        "days",
        "focused",
        "focus",
        "keep",
        "ict",
        "your",
        "them",
        "build"
    };

    public async Task<AiTradeDetailDto> GetTradeDetailForAiAsync(int tradeHistoryId, CancellationToken cancellationToken)
    {
        var trade = await context.TradeHistories
            .AsNoTracking()
            .Include(th => th.TradeEmotionTags)
            .Include(th => th.TradeChecklists)
                .ThenInclude(tc => tc.PretradeChecklist)
            .Include(th => th.TradeTechnicalAnalysisTags)
                .ThenInclude(tag => tag.TechnicalAnalysis)
            .Include(th => th.TradingZone)
            .Include(th => th.TradeScreenShots)
            .AsSplitQuery()
            .FirstOrDefaultAsync(th => th.Id == tradeHistoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Trade history not found: {tradeHistoryId}");

        // Resolve emotion tag names
        List<EmotionTagCacheDto> allEmotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);
        Dictionary<int, string> emotionNamesById = allEmotionTags
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        List<string> emotionTagNames = [.. trade.TradeEmotionTags?
            .Select(t => emotionNamesById.TryGetValue(t.EmotionTagId, out string? name) ? name : string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n)) ?? []];

        // Resolve checklist names
        List<string> checklistNames = [.. trade.TradeChecklists
            .Select(c => c.PretradeChecklist?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))];

        // Resolve technical analysis tag names
        List<string> technicalTags = [.. trade.TradeTechnicalAnalysisTags
            .Select(t => t.TechnicalAnalysis?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))];

        // Screenshot URLs
        List<string> screenshotUrls = [.. trade.TradeScreenShots
            .Select(s => s.Url)
            .Where(u => !string.IsNullOrWhiteSpace(u))];

        // Psychology notes
        List<string> psychologyNotes = await psychologyProvider
            .GetPsychologyByDate(trade.Date, cancellationToken);

        return new AiTradeDetailDto
        {
            TradeHistoryId = trade.Id,
            Asset = trade.Asset,
            Position = trade.Position.ToString(),
            EntryPrice = trade.EntryPrice,
            TargetTier1 = trade.TargetTier1,
            TargetTier2 = trade.TargetTier2,
            TargetTier3 = trade.TargetTier3,
            StopLoss = trade.StopLoss,
            Notes = trade.Notes ?? string.Empty,
            ExitPrice = trade.ExitPrice,
            Pnl = trade.Pnl,
            ConfidenceLevel = trade.ConfidenceLevel.ToString(),
            TradingZone = trade.TradingZone?.Name ?? string.Empty,
            OpenDate = trade.Date,
            ClosedDate = trade.ClosedDate ?? trade.Date,
            TechnicalAnalysisTags = technicalTags,
            EmotionTags = emotionTagNames,
            ChecklistItems = checklistNames,
            ScreenshotUrls = screenshotUrls,
            PsychologyNotes = psychologyNotes,
        };
    }

    public Task<ReviewSnapshot> BuildReviewSnapshotAsync(
        ReviewPeriodType periodType,
        DateTime referenceDate,
        int userId,
        CancellationToken cancellationToken)
    {
        return snapshotBuilder.BuildAsync(periodType, referenceDate, userId, cancellationToken);
    }

    public async Task<LessonSuggestionContextDto> GetLessonSuggestionContextAsync(
        DateTime? fromDate,
        DateTime? toDate,
        int userId,
        int maxTrades,
        CancellationToken cancellationToken)
    {
        DateTime? start = fromDate?.Date;
        DateTime? end = toDate?.Date.AddDays(1).AddTicks(-1);
        int safeMaxTrades = Math.Clamp(maxTrades, 1, 100);

        List<TradeHistory> trades = await context.TradeHistories
            .AsNoTracking()
            .Where(trade => trade.CreatedBy == userId)
            .Where(trade => trade.Status == TradeStatus.Closed && trade.Pnl.HasValue && trade.ClosedDate.HasValue)
            .Where(trade => !start.HasValue || trade.ClosedDate!.Value >= start.Value)
            .Where(trade => !end.HasValue || trade.ClosedDate!.Value <= end.Value)
            .Include(trade => trade.TradeEmotionTags)
            .Include(trade => trade.TradeTechnicalAnalysisTags)
                .ThenInclude(tag => tag.TechnicalAnalysis)
            .Include(trade => trade.TradingZone)
            .OrderByDescending(trade => trade.ClosedDate)
            .Take(safeMaxTrades)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        Dictionary<int, string> emotionNamesById = (await emotionTagProvider.GetEmotionTagsAsync(cancellationToken))
            .GroupBy(tag => tag.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        List<LessonSuggestionTradeDto> tradeContext = [.. trades.Select(trade => new LessonSuggestionTradeDto
        {
            TradeId = trade.Id,
            Asset = trade.Asset,
            Position = trade.Position.ToString(),
            Pnl = trade.Pnl ?? 0,
            ClosedDate = trade.ClosedDate ?? trade.Date,
            IsRuleBroken = trade.IsRuleBroken,
            Notes = trade.Notes ?? string.Empty,
            TradingZone = trade.TradingZone?.Name ?? "Unknown zone",
            EmotionTags = [.. (trade.TradeEmotionTags ?? [])
                .Select(tag => emotionNamesById.TryGetValue(tag.EmotionTagId, out string? name) ? name : string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))],
            TechnicalThemes = [.. (trade.TradeTechnicalAnalysisTags ?? [])
                .Select(tag => tag.TechnicalAnalysis?.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))],
        })];

        List<LessonLearned> lessons = await context.LessonsLearned
            .AsNoTracking()
            .Where(lesson => lesson.CreatedBy == userId)
            .Include(lesson => lesson.LessonTradeLinks)
            .OrderByDescending(lesson => lesson.CreatedDate)
            .Take(50)
            .ToListAsync(cancellationToken);

        List<ExistingLessonContextDto> existingLessons = [.. lessons.Select(lesson => new ExistingLessonContextDto
        {
            LessonId = lesson.Id,
            Title = lesson.Title,
            Category = (int)lesson.Category,
            KeyTakeaway = lesson.KeyTakeaway,
            LinkedTradeIds = [.. lesson.LessonTradeLinks
                .Where(link => !link.IsDisabled)
                .Select(link => link.TradeHistoryId)
                .Distinct()],
        })];

        return new LessonSuggestionContextDto
        {
            SampleSize = tradeContext.Count,
            RangeSummary = BuildRangeSummary(start, end),
            Trades = tradeContext,
            ExistingLessons = existingLessons,
        };
    }

    public async Task<LessonKnowledgeContextDto> GetLessonKnowledgeContextAsync(
        int userId,
        string? focusQuery,
        int maxLessons,
        CancellationToken cancellationToken)
    {
        int safeMaxLessons = Math.Clamp(maxLessons, 1, 10);
        List<string> focusTerms = ExtractKnowledgeTerms(focusQuery);

        IQueryable<LessonLearned> baseLessonQuery = context.LessonsLearned
            .AsNoTracking()
            .Where(lesson => lesson.CreatedBy == userId)
            .Where(lesson => !lesson.IsDisabled && lesson.Status != LessonStatus.Archived)
            .Include(lesson => lesson.LessonTradeLinks);

        IQueryable<LessonLearned> candidateQuery = baseLessonQuery;

        if (focusTerms.Count > 0)
        {
            candidateQuery = candidateQuery.Where(lesson => focusTerms.Any(term =>
                lesson.Title.Contains(term) ||
                lesson.Content.Contains(term) ||
                (lesson.KeyTakeaway != null && lesson.KeyTakeaway.Contains(term)) ||
                (lesson.ActionItems != null && lesson.ActionItems.Contains(term))));
        }

        List<LessonLearned> lessons = await candidateQuery
            .OrderByDescending(lesson => lesson.ImpactScore)
            .ThenByDescending(lesson => lesson.CreatedDate)
            .Take(MaxKnowledgeCandidateLessons)
            .ToListAsync(cancellationToken);

        if (lessons.Count == 0 && focusTerms.Count > 0)
        {
            lessons = await baseLessonQuery
                .OrderByDescending(lesson => lesson.ImpactScore)
                .ThenByDescending(lesson => lesson.CreatedDate)
                .Take(MaxKnowledgeCandidateLessons)
                .ToListAsync(cancellationToken);
        }

        List<LessonKnowledgeItemDto> selectedLessons = [.. lessons
            .Select(lesson => new
            {
                Lesson = lesson,
                Score = CalculateKnowledgeScore(lesson, focusTerms)
            })
            .Where(item => focusTerms.Count == 0 || item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Lesson.ImpactScore)
            .ThenByDescending(item => item.Lesson.CreatedDate)
            .Take(safeMaxLessons)
            .Select(item => new LessonKnowledgeItemDto
            {
                LessonId = item.Lesson.Id,
                Title = item.Lesson.Title,
                Category = item.Lesson.Category.ToString(),
                Severity = item.Lesson.Severity.ToString(),
                Status = item.Lesson.Status.ToString(),
                Tags = item.Lesson.Tags,
                Content = item.Lesson.Content,
                KeyTakeaway = item.Lesson.KeyTakeaway,
                ActionItems = item.Lesson.ActionItems,
                ImpactScore = item.Lesson.ImpactScore,
                LinkedTradeIds = [.. item.Lesson.LessonTradeLinks
                    .Where(link => !link.IsDisabled)
                    .Select(link => link.TradeHistoryId)
                    .Distinct()]
            })];

        if (selectedLessons.Count == 0)
        {
            selectedLessons = [.. lessons
                .OrderByDescending(lesson => lesson.ImpactScore)
                .ThenByDescending(lesson => lesson.CreatedDate)
                .Take(safeMaxLessons)
                .Select(lesson => new LessonKnowledgeItemDto
                {
                    LessonId = lesson.Id,
                    Title = lesson.Title,
                    Category = lesson.Category.ToString(),
                    Severity = lesson.Severity.ToString(),
                    Status = lesson.Status.ToString(),
                    Tags = lesson.Tags,
                    Content = lesson.Content,
                    KeyTakeaway = lesson.KeyTakeaway,
                    ActionItems = lesson.ActionItems,
                    ImpactScore = lesson.ImpactScore,
                    LinkedTradeIds = [.. lesson.LessonTradeLinks
                        .Where(link => !link.IsDisabled)
                        .Select(link => link.TradeHistoryId)
                        .Distinct()]
                })];
        }

        return new LessonKnowledgeContextDto
        {
            FocusQuery = string.IsNullOrWhiteSpace(focusQuery)
                ? "General ICT research context."
                : focusQuery.Trim(),
            Lessons = selectedLessons,
        };
    }

    public async Task<ResearchKnowledgeContextDto> GetResearchKnowledgeContextAsync(
        int userId,
        string? focusQuery,
        CancellationToken cancellationToken)
    {
        LessonKnowledgeContextDto lessonContext = await GetLessonKnowledgeContextAsync(
            userId,
            focusQuery,
            6,
            cancellationToken);

        List<string> focusTerms = ExtractKnowledgeTerms(focusQuery);
        List<PlaybookKnowledgeItemDto> playbookCandidates = await setupProvider.GetPlaybookKnowledgeItemsAsync(userId, cancellationToken);
        List<DailyNoteKnowledgeItemDto> dailyNoteCandidates = await psychologyProvider.GetDailyNoteKnowledgeItemsAsync(userId, cancellationToken);

        List<PlaybookKnowledgeItemDto> selectedPlaybooks = [.. playbookCandidates
            .Select(playbook => new
            {
                Item = playbook,
                Score = CalculateKnowledgeScore(
                    focusTerms,
                    (playbook.Name, 4),
                    (playbook.EntryRules, 3),
                    (playbook.IdealMarketConditions, 2),
                    (playbook.Description, 2),
                    (playbook.ExitRules, 1),
                    (playbook.PreferredAssets, 1),
                    (playbook.PreferredTimeframes, 1))
            })
            .Where(item => focusTerms.Count == 0 || item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Item.Status)
            .ThenByDescending(item => item.Item.TargetRiskReward ?? 0)
            .Take(MaxResearchPlaybooks)
            .Select(item => item.Item)];

        if (selectedPlaybooks.Count == 0)
        {
            selectedPlaybooks = [.. playbookCandidates
                .Take(MaxResearchPlaybooks)];
        }

        List<DailyNoteKnowledgeItemDto> selectedDailyNotes = [.. dailyNoteCandidates
            .Select(note => new
            {
                Item = note,
                Score = CalculateKnowledgeScore(
                    focusTerms,
                    (note.SessionFocus, 3),
                    (note.MarketStructureNotes, 3),
                    (note.KeyLevelsAndLiquidity, 2),
                    (note.KeyRulesAndReminders, 2),
                    (note.DailyBias, 1),
                    (note.NewsAndEvents, 1),
                    (note.MentalState, 1))
            })
            .Where(item => focusTerms.Count == 0 || item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Item.NoteDate)
            .Take(MaxResearchDailyNotes)
            .Select(item => item.Item)];

        if (selectedDailyNotes.Count == 0)
        {
            selectedDailyNotes = [.. dailyNoteCandidates
                .OrderByDescending(note => note.NoteDate)
                .Take(MaxResearchDailyNotes)];
        }

        return new ResearchKnowledgeContextDto
        {
            FocusQuery = lessonContext.FocusQuery,
            Lessons = lessonContext.Lessons,
            Playbooks = selectedPlaybooks,
            DailyNotes = selectedDailyNotes,
        };
    }

    public async Task<ReviewTradesPageDto> GetReviewTradesAsync(
        DateTime fromDate, DateTime toDate, int userId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<TradeHistory> query = context.TradeHistories
            .Where(t => t.CreatedBy == userId && t.Date >= fromDate && t.Date <= toDate)
            .AsNoTracking();

        int totalItems = await query.CountAsync(cancellationToken);

        List<TradeHistory> trades = await query
            .OrderByDescending(t => t.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.TradingZone)
            .ToListAsync(cancellationToken);

        List<int> tradeIds = [.. trades.Select(t => t.Id)];

        // Batch-load emotion tags
        List<TradeEmotionTag> emotionTags = await context.TradeEmotionTags
            .AsNoTracking()
            .Where(e => tradeIds.Contains(e.TradeHistoryId))
            .ToListAsync(cancellationToken);

        List<EmotionTagCacheDto> cachedEmotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);
        Dictionary<int, string> emotionLookup = cachedEmotionTags.ToDictionary(e => e.Id, e => e.Name);
        ILookup<int, int> emotionsByTrade = emotionTags.ToLookup(e => e.TradeHistoryId, e => e.EmotionTagId);

        // Batch-load technical analysis tags
        List<TradeTechnicalAnalysisTag> techTags = await context.TradeTechnicalAnalysisTags
            .AsNoTracking()
            .Where(t => tradeIds.Contains(t.TradeHistoryId))
            .Include(t => t.TechnicalAnalysis)
            .ToListAsync(cancellationToken);

        ILookup<int, string> techThemesByTrade = techTags
            .Where(t => t.TechnicalAnalysis != null)
            .ToLookup(t => t.TradeHistoryId, t => t.TechnicalAnalysis!.Name);

        // Batch-load checklist items
        List<TradeHistoryChecklist> checklists = await context.TradeHistoryChecklist
            .AsNoTracking()
            .Where(c => tradeIds.Contains(c.TradeHistoryId))
            .Include(c => c.PretradeChecklist)
            .ToListAsync(cancellationToken);

        ILookup<int, string> checklistsByTrade = checklists
            .Where(c => c.PretradeChecklist != null)
            .ToLookup(c => c.TradeHistoryId, c => c.PretradeChecklist!.Name);

        Dictionary<int, string> setupNamesById = (await setupProvider.GetSetupsAsync(userId, cancellationToken))
            .ToDictionary(setup => setup.Id, setup => setup.Name);

        List<ReviewTradeDto> items = [.. trades.Select(t => new ReviewTradeDto(
            t.Id, t.Asset, t.Position.ToString(), t.Pnl, t.Date, t.ClosedDate,
            t.EntryPrice, t.ExitPrice, (int)t.ConfidenceLevel,
            t.TradingZone?.Name,
            t.TradingSetupId.HasValue && setupNamesById.TryGetValue(t.TradingSetupId.Value, out string? setupName)
                ? setupName
                : null,
            t.IsRuleBroken, t.RuleBreakReason, t.Notes,
            [.. emotionsByTrade[t.Id].Where(emotionLookup.ContainsKey).Select(id => emotionLookup[id])],
            [.. techThemesByTrade[t.Id]],
            [.. checklistsByTrade[t.Id]]))];

        return new ReviewTradesPageDto(items, totalItems, (page * pageSize) < totalItems);
    }

    private static string BuildRangeSummary(DateTime? start, DateTime? end)
    {
        if (!start.HasValue && !end.HasValue)
        {
            return "All available closed trades.";
        }

        return $"Closed trades from {(start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "the beginning")} to {(end?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "now")}.";
    }

    private static List<string> ExtractKnowledgeTerms(string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return [];
        }

        return [.. focusQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeKnowledgeTerm)
            .Where(term => term.Length >= 3 && !KnowledgeStopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)];
    }

    private static string NormalizeKnowledgeTerm(string rawTerm)
    {
        return new string(rawTerm
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
    }

    private static int CalculateKnowledgeScore(LessonLearned lesson, IReadOnlyCollection<string> focusTerms)
    {
        return CalculateKnowledgeScore(
            focusTerms,
            (lesson.Title, 4),
            (lesson.KeyTakeaway, 3),
            (lesson.Content, 2),
            (lesson.ActionItems, 1));
    }

    private static int CalculateKnowledgeScore(
        IReadOnlyCollection<string> focusTerms,
        params (string? Text, int Weight)[] weightedSegments)
    {
        if (focusTerms.Count == 0)
        {
            return 0;
        }

        int score = 0;

        foreach ((string? Text, int Weight) weightedSegment in weightedSegments)
        {
            if (string.IsNullOrWhiteSpace(weightedSegment.Text) || weightedSegment.Weight <= 0)
            {
                continue;
            }

            HashSet<string> segmentTerms = ExtractKnowledgeTerms(weightedSegment.Text)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string term in focusTerms)
            {
                if (segmentTerms.Contains(term))
                {
                    score += weightedSegment.Weight;
                }
            }
        }

        return score;
    }
}
