namespace TradingJournal.Modules.Trades.Features.V1.Lessons;

public sealed class GetLessonsDashboard
{
    public sealed record Request(int UserId = 0) : IQuery<Result<LessonsDashboardViewModel>>;

    public sealed class Handler(ITradeDbContext context)
        : IQueryHandler<Request, Result<LessonsDashboardViewModel>>
    {
        public async Task<Result<LessonsDashboardViewModel>> Handle(Request request, CancellationToken cancellationToken)
        {
            int userId = request.UserId;
            DateTime now = DateTime.UtcNow;
            DateTime thirtyDaysAgo = now.AddDays(-30);
            DateTime sixtyDaysAgo = now.AddDays(-60);

            // ── Lesson Stats ──
            List<LessonLearned> allLessons = await context.LessonsLearned
                .AsNoTracking()
                .Where(l => l.CreatedBy == userId)
                .ToListAsync(cancellationToken);

            int totalLessons = allLessons.Count;
            int activeLessons = allLessons.Count(l => l.Status != LessonStatus.Archived);
            int appliedLessons = allLessons.Count(l => l.Status == LessonStatus.Applied);
            int criticalLessons = allLessons.Count(l => l.Severity == LessonSeverity.Critical && l.Status != LessonStatus.Applied);

            // ── Category Breakdown ──
            List<CategoryBreakdownItem> categoryBreakdown = [.. allLessons
                .GroupBy(l => l.Category)
                .Select(g => new CategoryBreakdownItem
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Percentage = totalLessons > 0 ? Math.Round((decimal)g.Count() / totalLessons * 100, 1) : 0
                })
                .OrderByDescending(c => c.Count)];

            // ── Discipline Logs (last 30 days) ──
            List<DisciplineLog> recentLogs = await context.DisciplineLogs
                .AsNoTracking()
                .Where(dl => dl.CreatedBy == userId && dl.Date >= thirtyDaysAgo)
                .ToListAsync(cancellationToken);

            int totalRulesChecked = recentLogs.Count;
            int totalRulesFollowed = recentLogs.Count(dl => dl.WasFollowed);
            int totalRulesBroken = totalRulesChecked - totalRulesFollowed;
            decimal disciplineScore = totalRulesChecked > 0
                ? Math.Round((decimal)totalRulesFollowed / totalRulesChecked * 100, 1)
                : 100;

            // ── Discipline Score Trend (vs previous 30 days) ──
            List<DisciplineLog> previousLogs = await context.DisciplineLogs
                .AsNoTracking()
                .Where(dl => dl.CreatedBy == userId && dl.Date >= sixtyDaysAgo && dl.Date < thirtyDaysAgo)
                .ToListAsync(cancellationToken);

            decimal previousScore = previousLogs.Count > 0
                ? Math.Round((decimal)previousLogs.Count(dl => dl.WasFollowed) / previousLogs.Count * 100, 1)
                : 100;

            decimal disciplineScoreTrend = disciplineScore - previousScore;

            // ── Discipline Timeline (weekly buckets, last 12 weeks) ──
            DateTime twelveWeeksAgo = now.AddDays(-84);
            List<DisciplineLog> timelineLogs = await context.DisciplineLogs
                .AsNoTracking()
                .Where(dl => dl.CreatedBy == userId && dl.Date >= twelveWeeksAgo)
                .ToListAsync(cancellationToken);

            List<DisciplineTimePoint> disciplineTimeline = [];
            for (int i = 11; i >= 0; i--)
            {
                DateTime weekStart = now.AddDays(-7 * (i + 1));
                DateTime weekEnd = now.AddDays(-7 * i);
                var weekLogs = timelineLogs.Where(dl => dl.Date >= weekStart && dl.Date < weekEnd).ToList();
                int followed = weekLogs.Count(dl => dl.WasFollowed);
                int total = weekLogs.Count;

                disciplineTimeline.Add(new DisciplineTimePoint
                {
                    Date = weekStart,
                    Score = total > 0 ? Math.Round((decimal)followed / total * 100, 1) : 100,
                    TotalChecks = total,
                    Followed = followed
                });
            }

            // ── Recent Lessons (top 5) ──
            List<int> recentLessonIds = allLessons
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .Select(l => l.Id)
                .ToList();

            Dictionary<int, int> linkCounts = await context.LessonTradeLinks
                .AsNoTracking()
                .Where(ltl => recentLessonIds.Contains(ltl.LessonLearnedId))
                .GroupBy(ltl => ltl.LessonLearnedId)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

            List<LessonLearnedViewModel> recentLessons = [.. allLessons
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .Select(l => new LessonLearnedViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Category = l.Category,
                    Severity = l.Severity,
                    Status = l.Status,
                    Tags = l.Tags,
                    KeyTakeaway = l.KeyTakeaway,
                    ImpactScore = l.ImpactScore,
                    LinkedTradesCount = linkCounts.GetValueOrDefault(l.Id, 0),
                    CreatedDate = l.CreatedDate
                })];

            // ── Top Impact Lessons ──
            List<LessonLearnedViewModel> topImpactLessons = [.. allLessons
                .Where(l => l.Status != LessonStatus.Archived)
                .OrderByDescending(l => l.ImpactScore)
                .ThenByDescending(l => l.Severity)
                .Take(5)
                .Select(l => new LessonLearnedViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Category = l.Category,
                    Severity = l.Severity,
                    Status = l.Status,
                    Tags = l.Tags,
                    KeyTakeaway = l.KeyTakeaway,
                    ImpactScore = l.ImpactScore,
                    LinkedTradesCount = linkCounts.GetValueOrDefault(l.Id, 0),
                    CreatedDate = l.CreatedDate
                })];

            // ── Linked Trade Loss Stats ──
            List<int> allLessonIds = allLessons.Select(l => l.Id).ToList();
            List<int> linkedTradeIds = await context.LessonTradeLinks
                .AsNoTracking()
                .Where(ltl => allLessonIds.Contains(ltl.LessonLearnedId))
                .Select(ltl => ltl.TradeHistoryId)
                .Distinct()
                .ToListAsync(cancellationToken);

            decimal totalLoss = 0;
            if (linkedTradeIds.Count > 0)
            {
                totalLoss = await context.TradeHistories
                    .AsNoTracking()
                    .Where(t => linkedTradeIds.Contains(t.Id) && t.Pnl.HasValue && t.Pnl < 0)
                    .SumAsync(t => t.Pnl ?? 0, cancellationToken);
            }

            return Result<LessonsDashboardViewModel>.Success(new LessonsDashboardViewModel
            {
                TotalLessons = totalLessons,
                ActiveLessons = activeLessons,
                AppliedLessons = appliedLessons,
                CriticalLessons = criticalLessons,
                CategoryBreakdown = categoryBreakdown,
                DisciplineScore = disciplineScore,
                DisciplineScoreTrend = disciplineScoreTrend,
                TotalRulesChecked = totalRulesChecked,
                TotalRulesFollowed = totalRulesFollowed,
                TotalRulesBroken = totalRulesBroken,
                DisciplineTimeline = disciplineTimeline,
                RecentLessons = recentLessons,
                TopImpactLessons = topImpactLessons,
                TotalLossFromLinkedTrades = totalLoss,
                LinkedTradesCount = linkedTradeIds.Count
            });
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Lessons);

            group.MapGet("/dashboard", async (ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request() with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .Produces<Result<LessonsDashboardViewModel>>(StatusCodes.Status200OK)
            .WithSummary("Get lessons & discipline dashboard.")
            .WithDescription("Aggregated dashboard data including lesson stats, discipline scoring, category breakdowns, and timeline.")
            .WithTags(Tags.Lessons)
            .RequireAuthorization();
        }
    }
}
