using System.Globalization;
using System.Text;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.AiInsights.Features.V1.Review;

public sealed class ExportReviewReport
{
    public sealed record Request(ReviewPeriodType PeriodType, DateTime PeriodStart, int UserId = 0)
        : IQuery<Result<ReviewReportFile>>;

    public sealed record ReviewReportFile(byte[] Content, string FileName, string ContentType);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.PeriodType).IsInEnum()
                .WithErrorCode(HttpStatusCode.BadRequest.ToString()).WithMessage("Invalid period type.");
        }
    }

    public sealed class Handler(IAiTradeDataProvider tradeDataProvider) : IQueryHandler<Request, Result<ReviewReportFile>>
    {
        public async Task<Result<ReviewReportFile>> Handle(Request request, CancellationToken cancellationToken)
        {
            ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
                request.PeriodType, request.PeriodStart, request.UserId, cancellationToken);

            string csv = BuildCsv(snapshot);
            byte[] content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            string fileName = BuildFileName(snapshot);
            return Result<ReviewReportFile>.Success(new ReviewReportFile(content, fileName, "text/csv"));
        }

        private static string BuildFileName(ReviewSnapshot snapshot)
        {
            string periodLabel = snapshot.PeriodType switch
            {
                ReviewPeriodType.Daily => snapshot.PeriodStart.ToString("yyyy-MM-dd"),
                ReviewPeriodType.Weekly => $"{snapshot.PeriodStart:yyyy-MM-dd}_to_{snapshot.PeriodEnd:yyyy-MM-dd}",
                ReviewPeriodType.Monthly => snapshot.PeriodStart.ToString("yyyy-MM"),
                ReviewPeriodType.Quarterly => $"{snapshot.PeriodStart.Year}-Q{((snapshot.PeriodStart.Month - 1) / 3) + 1}",
                _ => snapshot.PeriodStart.ToString("yyyy-MM-dd"),
            };
            return $"trading-report_{snapshot.PeriodType.ToString().ToLowerInvariant()}_{periodLabel}.csv";
        }

        private static string BuildCsv(ReviewSnapshot snapshot)
        {
            StringBuilder sb = new();
            ReviewSnapshotMetrics metrics = snapshot.Metrics;
            sb.AppendLine("=== TRADING REPORT ===");
            sb.AppendLine();
            sb.AppendLine($"Period Type,{snapshot.PeriodType}");
            sb.AppendLine($"Period Start,{snapshot.PeriodStart:yyyy-MM-dd}");
            sb.AppendLine($"Period End,{snapshot.PeriodEnd:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine("=== PERFORMANCE SUMMARY ===");
            sb.AppendLine();
            sb.AppendLine($"Total Trades,{metrics.TotalTrades}");
            sb.AppendLine($"Wins,{metrics.Wins}");
            sb.AppendLine($"Losses,{metrics.Losses}");
            sb.AppendLine($"Win Rate,{metrics.WinRate:F1}%");
            sb.AppendLine($"Total P&L,{metrics.TotalPnl:F2}");
            sb.AppendLine($"Average Win,{metrics.AverageWin:F2}");
            sb.AppendLine($"Average Loss,{metrics.AverageLoss:F2}");
            sb.AppendLine($"Best Trade P&L,{metrics.BestTradePnl:F2}");
            sb.AppendLine($"Worst Trade P&L,{metrics.WorstTradePnl:F2}");
            sb.AppendLine($"Best Day P&L,{metrics.BestDayPnl:F2}");
            sb.AppendLine($"Worst Day P&L,{metrics.WorstDayPnl:F2}");
            sb.AppendLine($"Long Trades,{metrics.LongTrades}");
            sb.AppendLine($"Short Trades,{metrics.ShortTrades}");
            sb.AppendLine($"Rule Break Trades,{metrics.RuleBreakTrades}");
            sb.AppendLine($"High Confidence Trades,{metrics.HighConfidenceTrades}");
            if (!string.IsNullOrWhiteSpace(metrics.TopAsset)) sb.AppendLine($"Top Asset,{metrics.TopAsset}");
            if (!string.IsNullOrWhiteSpace(metrics.PrimaryTradingZone)) sb.AppendLine($"Primary Trading Zone,{metrics.PrimaryTradingZone}");
            if (!string.IsNullOrWhiteSpace(metrics.DominantEmotion)) sb.AppendLine($"Dominant Emotion,{metrics.DominantEmotion}");
            if (!string.IsNullOrWhiteSpace(metrics.TopTechnicalTheme)) sb.AppendLine($"Top Technical Theme,{metrics.TopTechnicalTheme}");
            sb.AppendLine();
            sb.AppendLine("=== TRADE DETAILS ===");
            sb.AppendLine();
            sb.AppendLine(string.Join(",", ["Trade ID","Open Date","Closed Date","Asset","Position","Entry Price","Exit Price","P&L","Confidence Level","Trading Zone","Rule Broken","Rule Break Reason","Emotion Tags","Technical Themes","Checklist Items","Notes"]));
            foreach (ReviewTradeInsight trade in snapshot.Trades)
            {
                sb.AppendLine(string.Join(",", [
                    trade.TradeId.ToString(CultureInfo.InvariantCulture),
                    trade.OpenDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    trade.ClosedDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    Esc(trade.Asset), trade.Position.ToString(),
                    trade.EntryPrice.ToString("F5", CultureInfo.InvariantCulture),
                    trade.ExitPrice?.ToString("F5", CultureInfo.InvariantCulture) ?? "",
                    trade.Pnl.ToString("F2", CultureInfo.InvariantCulture),
                    trade.ConfidenceLevel.ToString(), Esc(trade.TradingZone ?? ""),
                    trade.IsRuleBroken ? "Yes" : "No", Esc(trade.RuleBreakReason ?? ""),
                    Esc(string.Join("; ", trade.EmotionTags)),
                    Esc(string.Join("; ", trade.TechnicalThemes)),
                    Esc(string.Join("; ", trade.ChecklistItems)),
                    Esc(trade.Notes ?? "")]));
            }
            if (snapshot.PsychologyNotes.Count > 0)
            {
                sb.AppendLine(); sb.AppendLine("=== PSYCHOLOGY NOTES ==="); sb.AppendLine();
                foreach (string note in snapshot.PsychologyNotes) sb.AppendLine(Esc(note));
            }
            return sb.ToString();
        }

        private static string Esc(string v) =>
            string.IsNullOrEmpty(v) ? "" :
            v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r')
                ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    public sealed class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.Reviews);
            group.MapGet("/export", async (ReviewPeriodType periodType, DateTime periodStart, ClaimsPrincipal user, ISender sender) =>
            {
                Result<ReviewReportFile> result = await sender.Send(new Request(periodType, periodStart) with { UserId = user.GetCurrentUserId() });
                if (!result.IsSuccess) return Results.Problem(result.Errors[0].Description);
                ReviewReportFile file = result.Value;
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .Produces<FileResult>(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest)
            .WithSummary("Export review report as CSV.")
            .WithDescription("Generates and downloads a CSV report containing summary metrics and all closed trades for the specified review period.")
            .WithTags(Tags.Reviews).RequireAuthorization();
        }
    }
}
