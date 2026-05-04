using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.Analytics.Features.V1;

public sealed class GetConceptPerformance
{
    internal sealed record Request(AnalyticsFilter Filter, int UserId = 0)
        : IQuery<Result<ConceptPerformanceResponse>>;

    internal sealed record ConceptMetricViewModel(
        int ConceptId,
        string ConceptName,
        int TotalTrades,
        int Wins,
        int Losses,
        decimal WinRate,
        decimal TotalPnl,
        decimal AvgPnl,
        decimal ProfitFactor,
        decimal Expectancy,
        decimal AvgRiskReward,
        string Grade);

    internal sealed record ConceptPerformanceResponse(
        IReadOnlyCollection<ConceptMetricViewModel> Concepts);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Filter)
                .IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Invalid filter value.");
        }
    }

    internal sealed class Handler(
        ITradeProvider tradeProvider,
        ITechnicalAnalysisTagProvider tagProvider) : IQueryHandler<Request, Result<ConceptPerformanceResponse>>
    {
        public async Task<Result<ConceptPerformanceResponse>> Handle(
            Request request, CancellationToken cancellationToken)
        {
            List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
            List<TechnicalAnalysisTagDto> tags = await tagProvider.GetTagsAsync(cancellationToken);
            DateTimeOffset fromDate = AnalyticsFilterHelper.GetFromDate(request.Filter);

            // Filter to closed trades that have at least one tag
            List<TradeCacheDto> closed = [.. trades
                .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
                .Where(t => t.TechnicalAnalysisTagIds is { Count: > 0 })
                .Where(t => fromDate == DateTimeOffset.MinValue || (t.ClosedDate.HasValue && t.ClosedDate.Value >= fromDate))];

            // Build tag name lookup
            Dictionary<int, TechnicalAnalysisTagDto> tagLookup = tags.ToDictionary(t => t.Id);

            // Flatten: each trade is counted once per tag it has
            var flattenedEntries = closed
                .SelectMany(trade => trade.TechnicalAnalysisTagIds!
                    .Select(tagId => new { TagId = tagId, Trade = trade }))
                .ToList();

            List<ConceptMetricViewModel> concepts = [.. flattenedEntries
                .GroupBy(e => e.TagId)
                .Select(g =>
                {
                    List<TradeCacheDto> groupTrades = [.. g.Select(e => e.Trade)];
                    List<TradeCacheDto> wins = groupTrades.Where(t => t.Pnl > 0).ToList();
                    List<TradeCacheDto> losses = groupTrades.Where(t => t.Pnl <= 0).ToList();

                    decimal totalPnl = groupTrades.Sum(t => t.Pnl!.Value);
                    decimal winRate = (decimal)wins.Count / groupTrades.Count * 100;
                    decimal avgPnl = groupTrades.Average(t => t.Pnl!.Value);

                    decimal avgWin = wins.Count > 0 ? wins.Average(t => t.Pnl!.Value) : 0;
                    decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => t.Pnl!.Value)) : 0;

                    decimal grossProfit = wins.Sum(t => t.Pnl!.Value);
                    decimal grossLoss = Math.Abs(losses.Sum(t => t.Pnl!.Value));
                    decimal profitFactor = grossLoss > 0
                        ? grossProfit / grossLoss
                        : (grossProfit > 0 ? decimal.MaxValue : 0);

                    decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);

                    double[] rrValues = groupTrades
                        .Where(t => t.StopLoss > 0 && t.TargetTier1 > 0 && t.EntryPrice > 0)
                        .Select(t =>
                        {
                            decimal risk = Math.Abs(t.EntryPrice - t.StopLoss);
                            decimal reward = Math.Abs(t.TargetTier1 - t.EntryPrice);
                            return risk > 0 ? (double)(reward / risk) : 0;
                        })
                        .Where(r => r > 0)
                        .ToArray();
                    decimal avgRiskReward = rrValues.Length > 0 ? (decimal)rrValues.Average() : 0;

                    string tagName = tagLookup.GetValueOrDefault(g.Key)?.Name ?? $"Concept #{g.Key}";

                    string grade = CalculateGrade(winRate, profitFactor, groupTrades.Count);

                    return new ConceptMetricViewModel(
                        g.Key,
                        tagName,
                        groupTrades.Count,
                        wins.Count,
                        losses.Count,
                        Math.Round(winRate, 1),
                        Math.Round(totalPnl, 2),
                        Math.Round(avgPnl, 2),
                        Math.Round(profitFactor, 2),
                        Math.Round(expectancy, 2),
                        Math.Round(avgRiskReward, 2),
                        grade);
                })
                .OrderByDescending(c => c.TotalPnl)];

            return Result<ConceptPerformanceResponse>.Success(
                new ConceptPerformanceResponse(concepts));
        }

        private static string CalculateGrade(decimal winRate, decimal profitFactor, int totalTrades)
        {
            if (totalTrades < 5) return "N/A";
            if (winRate >= 65 && profitFactor >= 2) return "A";
            if (winRate >= 55 && profitFactor >= 1.5m) return "B";
            if (winRate >= 45 && profitFactor >= 1) return "C";
            if (winRate >= 35) return "D";
            return "F";
        }
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup("api/v1/analytics");

            group.MapGet("/concept-performance", async (AnalyticsFilter filter, ClaimsPrincipal user, ISender sender) =>
            {
                var result = await sender.Send(new Request(filter) with { UserId = user.GetCurrentUserId() });
                return result.IsSuccess ? Results.Ok(result) : Results.Problem(result.Errors[0].Description);
            })
            .Produces<Result<ConceptPerformanceResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Get ICT concept performance breakdown.")
            .WithDescription("Retrieves performance metrics grouped by technical analysis tags (ICT concepts like FVG, OB, BOS, etc.).")
            .WithTags(Tags.Analytics)
            .RequireAuthorization();
        }
    }
}
