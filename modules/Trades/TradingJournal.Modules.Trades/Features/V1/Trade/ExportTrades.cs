using ClosedXML.Excel;
using System.Globalization;
using System.Text;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Features.V1.Trade;

public class ExportTrades
{
    public enum ExportFormat
    {
        Csv = 0,
        Excel = 1
    }

    public class Request : IQuery<Result<ExportResult>>
    {
        public string? Asset { get; set; }
        public PositionType? Position { get; set; }
        public TradeStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public ExportFormat Format { get; set; } = ExportFormat.Csv;
        public int UserId { get; set; }
    }

    public class ExportResult
    {
        public byte[] FileContent { get; set; } = [];
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    public sealed class Handler(
        ITradeDbContext tradeDbContext,
        IEmotionTagProvider emotionTagProvider) : IQueryHandler<Request, Result<ExportResult>>
    {
        public async Task<Result<ExportResult>> Handle(Request request, CancellationToken cancellationToken)
        {
            // Query trades with the same filters as GetTrades but without pagination
            IQueryable<TradeHistory> query = tradeDbContext.TradeHistories
                .Where(th => th.CreatedBy == request.UserId)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(request.Asset))
                query = query.Where(th => th.Asset.Contains(request.Asset));

            if (request.Position.HasValue)
                query = query.Where(th => th.Position == request.Position.Value);

            if (request.Status.HasValue)
                query = query.Where(th => th.Status == request.Status.Value);

            if (request.FromDate.HasValue)
                query = query.Where(th => th.Date >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(th => th.Date <= request.ToDate.Value);

            List<TradeHistory> trades = await query
                .OrderByDescending(th => th.Date)
                .Include(th => th.TradingZone)
                .Include(th => th.TradingSession)
                .ToListAsync(cancellationToken);

            // Batch-fetch emotion tags
            List<int> tradeIds = [.. trades.Select(t => t.Id)];

            List<TradeEmotionTag> tradeEmotionTags = await tradeDbContext.TradeEmotionTags
                .AsNoTracking()
                .Where(tet => tradeIds.Contains(tet.TradeHistoryId))
                .ToListAsync(cancellationToken);

            List<EmotionTagCacheDto> cachedEmotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);
            Dictionary<int, string> emotionTagLookup = cachedEmotionTags.ToDictionary(e => e.Id, e => e.Name);
            ILookup<int, int> emotionTagIdsByTrade = tradeEmotionTags
                .ToLookup(tet => tet.TradeHistoryId, tet => tet.EmotionTagId);

            // Build export rows
            List<TradeExportRow> rows = trades.Select(t => new TradeExportRow
            {
                Id = t.Id,
                Asset = t.Asset,
                Position = t.Position.ToString(),
                Status = t.Status.ToString(),
                Date = t.Date,
                EntryPrice = t.EntryPrice,
                ExitPrice = t.ExitPrice,
                Pnl = t.Pnl,
                ClosedDate = t.ClosedDate,
                StopLoss = t.StopLoss,
                TargetTier1 = t.TargetTier1,
                TargetTier2 = t.TargetTier2,
                TargetTier3 = t.TargetTier3,
                HitStopLoss = t.HitStopLoss,
                ConfidenceLevel = t.ConfidenceLevel.ToString(),
                IsRuleBroken = t.IsRuleBroken,
                RuleBreakReason = t.RuleBreakReason,
                TradingZone = t.TradingZone?.Name,
                TradingSession = t.TradingSession != null
                    ? t.TradingSession.FromTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : null,
                Notes = t.Notes,
                EmotionTags = string.Join("; ", emotionTagIdsByTrade[t.Id]
                    .Where(emotionTagLookup.ContainsKey)
                    .Select(id => emotionTagLookup[id])),
                PowerOf3Phase = t.PowerOf3Phase?.ToString(),
                DailyBias = t.DailyBias?.ToString(),
                MarketStructure = t.MarketStructure?.ToString(),
                PremiumDiscount = t.PremiumDiscount?.ToString()
            }).ToList();

            ExportResult result = request.Format == ExportFormat.Excel
                ? GenerateExcel(rows)
                : GenerateCsv(rows);

            return Result<ExportResult>.Success(result);
        }

        private static ExportResult GenerateCsv(List<TradeExportRow> rows)
        {
            StringBuilder sb = new();

            // Header
            sb.AppendLine(string.Join(",", [
                "ID", "Asset", "Position", "Status", "Date", "Entry Price", "Exit Price",
                "P&L", "Closed Date", "Stop Loss", "Target T1", "Target T2", "Target T3",
                "Hit Stop Loss", "Confidence", "Rule Broken", "Rule Break Reason",
                "Trading Zone", "Trading Session", "Emotions", "Notes",
                "Power of 3 Phase", "Daily Bias", "Market Structure", "Premium/Discount"
            ]));

            // Data rows
            foreach (TradeExportRow row in rows)
            {
                sb.AppendLine(string.Join(",", [
                    row.Id.ToString(),
                    CsvEscape(row.Asset),
                    row.Position,
                    row.Status,
                    row.Date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    row.EntryPrice.ToString(CultureInfo.InvariantCulture),
                    row.ExitPrice?.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.Pnl?.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.ClosedDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
                    row.StopLoss.ToString(CultureInfo.InvariantCulture),
                    row.TargetTier1.ToString(CultureInfo.InvariantCulture),
                    row.TargetTier2?.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.TargetTier3?.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.HitStopLoss?.ToString() ?? "",
                    row.ConfidenceLevel,
                    row.IsRuleBroken.ToString(),
                    CsvEscape(row.RuleBreakReason),
                    CsvEscape(row.TradingZone),
                    CsvEscape(row.TradingSession),
                    CsvEscape(row.EmotionTags),
                    CsvEscape(row.Notes),
                    row.PowerOf3Phase ?? "",
                    row.DailyBias ?? "",
                    row.MarketStructure ?? "",
                    row.PremiumDiscount ?? ""
                ]));
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            return new ExportResult
            {
                FileContent = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                FileName = $"trades_export_{timestamp}.csv",
                ContentType = "text/csv"
            };
        }

        private static ExportResult GenerateExcel(List<TradeExportRow> rows)
        {
            using XLWorkbook workbook = new();
            IXLWorksheet ws = workbook.Worksheets.Add("Trades");

            // Headers
            string[] headers = [
                "ID", "Asset", "Position", "Status", "Date", "Entry Price", "Exit Price",
                "P&L", "Closed Date", "Stop Loss", "Target T1", "Target T2", "Target T3",
                "Hit Stop Loss", "Confidence", "Rule Broken", "Rule Break Reason",
                "Trading Zone", "Trading Session", "Emotions", "Notes",
                "Power of 3 Phase", "Daily Bias", "Market Structure", "Premium/Discount"
            ];

            for (int i = 0; i < headers.Length; i++)
            {
                IXLCell cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); // slate-800
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            // Data rows
            for (int r = 0; r < rows.Count; r++)
            {
                TradeExportRow row = rows[r];
                int rowIdx = r + 2;

                ws.Cell(rowIdx, 1).Value = row.Id;
                ws.Cell(rowIdx, 2).Value = row.Asset;
                ws.Cell(rowIdx, 3).Value = row.Position;
                ws.Cell(rowIdx, 4).Value = row.Status;
                ws.Cell(rowIdx, 5).Value = row.Date;
                ws.Cell(rowIdx, 5).Style.NumberFormat.Format = "yyyy-MM-dd HH:mm:ss";
                ws.Cell(rowIdx, 6).Value = row.EntryPrice;
                ws.Cell(rowIdx, 6).Style.NumberFormat.Format = "#,##0.00###";

                if (row.ExitPrice.HasValue)
                {
                    ws.Cell(rowIdx, 7).Value = row.ExitPrice.Value;
                    ws.Cell(rowIdx, 7).Style.NumberFormat.Format = "#,##0.00###";
                }

                if (row.Pnl.HasValue)
                {
                    ws.Cell(rowIdx, 8).Value = row.Pnl.Value;
                    ws.Cell(rowIdx, 8).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(rowIdx, 8).Style.Font.FontColor = row.Pnl.Value >= 0
                        ? XLColor.FromArgb(34, 197, 94)   // green
                        : XLColor.FromArgb(239, 68, 68);  // red
                }

                if (row.ClosedDate.HasValue)
                {
                    ws.Cell(rowIdx, 9).Value = row.ClosedDate.Value;
                    ws.Cell(rowIdx, 9).Style.NumberFormat.Format = "yyyy-MM-dd HH:mm:ss";
                }

                ws.Cell(rowIdx, 10).Value = row.StopLoss;
                ws.Cell(rowIdx, 10).Style.NumberFormat.Format = "#,##0.00###";
                ws.Cell(rowIdx, 11).Value = row.TargetTier1;
                ws.Cell(rowIdx, 11).Style.NumberFormat.Format = "#,##0.00###";

                if (row.TargetTier2.HasValue)
                {
                    ws.Cell(rowIdx, 12).Value = row.TargetTier2.Value;
                    ws.Cell(rowIdx, 12).Style.NumberFormat.Format = "#,##0.00###";
                }

                if (row.TargetTier3.HasValue)
                {
                    ws.Cell(rowIdx, 13).Value = row.TargetTier3.Value;
                    ws.Cell(rowIdx, 13).Style.NumberFormat.Format = "#,##0.00###";
                }

                if (row.HitStopLoss.HasValue)
                    ws.Cell(rowIdx, 14).Value = row.HitStopLoss.Value ? "Yes" : "No";

                ws.Cell(rowIdx, 15).Value = row.ConfidenceLevel;
                ws.Cell(rowIdx, 16).Value = row.IsRuleBroken ? "Yes" : "No";
                ws.Cell(rowIdx, 17).Value = row.RuleBreakReason ?? "";
                ws.Cell(rowIdx, 18).Value = row.TradingZone ?? "";
                ws.Cell(rowIdx, 19).Value = row.TradingSession ?? "";
                ws.Cell(rowIdx, 20).Value = row.EmotionTags ?? "";
                ws.Cell(rowIdx, 21).Value = row.Notes ?? "";
                ws.Cell(rowIdx, 22).Value = row.PowerOf3Phase ?? "";
                ws.Cell(rowIdx, 23).Value = row.DailyBias ?? "";
                ws.Cell(rowIdx, 24).Value = row.MarketStructure ?? "";
                ws.Cell(rowIdx, 25).Value = row.PremiumDiscount ?? "";

                // Alternate row coloring for readability
                if (r % 2 == 1)
                {
                    IXLRange rowRange = ws.Range(rowIdx, 1, rowIdx, headers.Length);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249); // slate-100
                }
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            // Freeze header row
            ws.SheetView.FreezeRows(1);

            // Add auto-filter
            if (rows.Count > 0)
                ws.RangeUsed()?.SetAutoFilter();

            // Summary sheet
            IXLWorksheet summary = workbook.Worksheets.Add("Summary");
            summary.Cell(1, 1).Value = "Trade Export Summary";
            summary.Cell(1, 1).Style.Font.Bold = true;
            summary.Cell(1, 1).Style.Font.FontSize = 14;

            summary.Cell(3, 1).Value = "Total Trades";
            summary.Cell(3, 2).Value = rows.Count;

            int winCount = rows.Count(r => r.Pnl.HasValue && r.Pnl.Value > 0);
            int lossCount = rows.Count(r => r.Pnl.HasValue && r.Pnl.Value < 0);
            int breakEvenCount = rows.Count(r => r.Pnl.HasValue && r.Pnl.Value == 0);

            summary.Cell(4, 1).Value = "Winning Trades";
            summary.Cell(4, 2).Value = winCount;
            summary.Cell(4, 2).Style.Font.FontColor = XLColor.FromArgb(34, 197, 94);

            summary.Cell(5, 1).Value = "Losing Trades";
            summary.Cell(5, 2).Value = lossCount;
            summary.Cell(5, 2).Style.Font.FontColor = XLColor.FromArgb(239, 68, 68);

            summary.Cell(6, 1).Value = "Break-even Trades";
            summary.Cell(6, 2).Value = breakEvenCount;

            summary.Cell(7, 1).Value = "Win Rate";
            int closedCount = winCount + lossCount + breakEvenCount;
            summary.Cell(7, 2).Value = closedCount > 0
                ? $"{(double)winCount / closedCount * 100:F1}%"
                : "N/A";

            decimal totalPnl = rows.Where(r => r.Pnl.HasValue).Sum(r => r.Pnl!.Value);
            summary.Cell(8, 1).Value = "Total P&L";
            summary.Cell(8, 2).Value = totalPnl;
            summary.Cell(8, 2).Style.NumberFormat.Format = "#,##0.00";
            summary.Cell(8, 2).Style.Font.FontColor = totalPnl >= 0
                ? XLColor.FromArgb(34, 197, 94)
                : XLColor.FromArgb(239, 68, 68);

            summary.Cell(10, 1).Value = "Export Date";
            summary.Cell(10, 2).Value = DateTime.UtcNow;
            summary.Cell(10, 2).Style.NumberFormat.Format = "yyyy-MM-dd HH:mm:ss";

            summary.Column(1).Style.Font.Bold = true;
            summary.Columns().AdjustToContents();

            using MemoryStream ms = new();
            workbook.SaveAs(ms);

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            return new ExportResult
            {
                FileContent = ms.ToArray(),
                FileName = $"trades_export_{timestamp}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    /// <summary>
    /// Flat row used for CSV/Excel export.
    /// </summary>
    private class TradeExportRow
    {
        public int Id { get; set; }
        public string Asset { get; set; } = "";
        public string Position { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal? Pnl { get; set; }
        public DateTime? ClosedDate { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TargetTier1 { get; set; }
        public decimal? TargetTier2 { get; set; }
        public decimal? TargetTier3 { get; set; }
        public bool? HitStopLoss { get; set; }
        public string ConfidenceLevel { get; set; } = "";
        public bool IsRuleBroken { get; set; }
        public string? RuleBreakReason { get; set; }
        public string? TradingZone { get; set; }
        public string? TradingSession { get; set; }
        public string? EmotionTags { get; set; }
        public string? Notes { get; set; }
        public string? PowerOf3Phase { get; set; }
        public string? DailyBias { get; set; }
        public string? MarketStructure { get; set; }
        public string? PremiumDiscount { get; set; }
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            RouteGroupBuilder group = app.MapGroup(ApiGroup.V1.TradeHistory);

            group.MapPost("/export", async (ISender sender, [FromBody] Request request, ClaimsPrincipal user) =>
            {
                request.UserId = user.GetCurrentUserId();
                Result<ExportResult> result = await sender.Send(request);

                if (!result.IsSuccess)
                    return Results.BadRequest(result);

                ExportResult export = result.Value;
                return Results.File(
                    export.FileContent,
                    export.ContentType,
                    export.FileName);
            })
            .Produces<byte[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithSummary("Export trade histories as CSV or Excel.")
            .WithDescription("Exports filtered trade histories as a downloadable CSV or Excel file.")
            .WithTags(Tags.TradeHistory)
            .RequireAuthorization();
        }
    }
}
