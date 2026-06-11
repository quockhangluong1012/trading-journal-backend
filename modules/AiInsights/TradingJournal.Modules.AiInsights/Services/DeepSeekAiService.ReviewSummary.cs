using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Extensions;
using TradingJournal.Modules.AiInsights.Options;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.AiInsights.Services;

internal sealed partial class DeepSeekAiService
{
    public async Task<ReviewSummaryResultDto?> GenerateReviewSummary(ReviewSummaryRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetReviewSummary();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Not Found Review Prompt File.");
        }

        ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
            request.PeriodType,
            request.PeriodStart,
            request.UserId,
            cancellationToken);
        ReviewSnapshotMetrics metrics = snapshot.Metrics;

        Dictionary<string, string> replacements = new()
        {
            { "{{PeriodType}}", request.PeriodType.ToString() },
            { "{{PeriodStart}}", FormatPromptDate(snapshot.PeriodStart) },
            { "{{PeriodEnd}}", FormatPromptDate(snapshot.PeriodEnd) },
            { "{{TotalPnl}}", FormatPromptNumber(metrics.TotalPnl, 2) },
            { "{{WinRate}}", FormatPromptNumber(metrics.WinRate, 1) },
            { "{{TotalTrades}}", metrics.TotalTrades.ToString() },
            { "{{Wins}}", metrics.Wins.ToString() },
            { "{{Losses}}", metrics.Losses.ToString() },
            { "{{AverageWin}}", FormatPromptNumber(metrics.AverageWin, 2) },
            { "{{AverageLoss}}", FormatPromptNumber(metrics.AverageLoss, 2) },
            { "{{BestTradePnl}}", FormatPromptNumber(metrics.BestTradePnl, 2) },
            { "{{WorstTradePnl}}", FormatPromptNumber(metrics.WorstTradePnl, 2) },
            { "{{BestDayPnl}}", FormatPromptNumber(metrics.BestDayPnl, 2) },
            { "{{WorstDayPnl}}", FormatPromptNumber(metrics.WorstDayPnl, 2) },
            { "{{LongTrades}}", metrics.LongTrades.ToString() },
            { "{{ShortTrades}}", metrics.ShortTrades.ToString() },
            { "{{RuleBreakTrades}}", metrics.RuleBreakTrades.ToString() },
            { "{{HighConfidenceTrades}}", metrics.HighConfidenceTrades.ToString() },
            { "{{TopAsset}}", metrics.TopAsset ?? "No data available" },
            { "{{PrimaryTradingZone}}", metrics.PrimaryTradingZone ?? "No data available" },
            { "{{DominantEmotion}}", metrics.DominantEmotion ?? "No data available" },
            { "{{TopTechnicalTheme}}", metrics.TopTechnicalTheme ?? "No data available" },
            { "{{TradeCaseStudies}}", BuildReviewTradeCaseStudies(snapshot.Trades) },
            { "{{TradesList}}", BuildReviewTradeList(snapshot.Trades) },
            { "{{PsychologyNotes}}", BuildPsychologyDigest(snapshot.PsychologyNotes) }
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);

        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);

        return ParseReviewAiResponse(responseText);
    }

    private static string BuildReviewTradeList(IReadOnlyList<ReviewTradeInsight> trades)
    {
        if (trades.Count == 0)
        {
            return "No closed trades in this review period.";
        }

        List<string> lines = [];

        if (trades.Count > 12)
        {
            lines.Add($"- Showing the 12 most recent trades out of {trades.Count} total closed trades. Performance metrics above reflect the entire review period.");
        }

        lines.AddRange(trades
            .OrderByDescending(trade => trade.ClosedDate)
            .Take(12)
            .Select(BuildTradeLine));

        return string.Join("\n", lines);
    }

    private static string BuildReviewTradeCaseStudies(IReadOnlyList<ReviewTradeInsight> trades)
    {
        if (trades.Count == 0)
        {
            return "No trade cases to analyze in this review period.";
        }

        List<string> sections = [];

        AppendTradeSection(sections, "Best trades",
            trades.Where(trade => trade.Pnl > 0).OrderByDescending(trade => trade.Pnl).Take(3));

        AppendTradeSection(sections, "Worst trades",
            trades.Where(trade => trade.Pnl <= 0).OrderBy(trade => trade.Pnl).Take(3));

        AppendTradeSection(sections, "Rule-break trades",
            trades.Where(trade => trade.IsRuleBroken).OrderByDescending(trade => Math.Abs(trade.Pnl)).Take(3));

        return sections.Count > 0
            ? string.Join("\n", sections)
            : "No trade cases to analyze in this review period.";
    }

    private static void AppendTradeSection(
        ICollection<string> sections,
        string title,
        IEnumerable<ReviewTradeInsight> trades)
    {
        List<ReviewTradeInsight> tradeList = [.. trades];

        if (tradeList.Count == 0)
        {
            return;
        }

        sections.Add($"### {title}");

        foreach (ReviewTradeInsight trade in tradeList)
        {
            sections.Add(BuildTradeLine(trade));
        }
    }

    private static string BuildTradeLine(ReviewTradeInsight trade)
    {
        string technicalThemes = JoinOrFallback(trade.TechnicalThemes);
        string emotionTags = JoinOrFallback(trade.EmotionTags);
        string checklistItems = JoinOrFallback(trade.ChecklistItems);
        string zone = string.IsNullOrWhiteSpace(trade.TradingZone) ? "Unknown zone" : trade.TradingZone;
        string notes = string.IsNullOrWhiteSpace(trade.Notes) ? "No note" : trade.Notes;
        string ruleBreak = trade.IsRuleBroken
            ? $"Yes ({(string.IsNullOrWhiteSpace(trade.RuleBreakReason) ? "No reason logged" : trade.RuleBreakReason)})"
            : "No";

        return $"- {FormatPromptDate(trade.ClosedDate)} | {trade.Asset} | {trade.Position} | PnL: {FormatPromptNumber(trade.Pnl, 2)} | Confidence: {trade.ConfidenceLevel} | Zone: {zone} | Rule break: {ruleBreak} | Technical: {technicalThemes} | Emotions: {emotionTags} | Checklist: {checklistItems} | Note: {notes}";
    }

    private static string BuildPsychologyDigest(IReadOnlyList<string> psychologyNotes)
    {
        return psychologyNotes.Count > 0
            ? string.Join("\n", psychologyNotes)
            : "No psychology journal entries in this review period.";
    }

    private static string JoinOrFallback(IReadOnlyList<string> values)
    {
        return values.Count > 0 ? string.Join(", ", values) : "None";
    }

    private static string FormatPromptDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatPromptNumber(decimal value, int decimals)
    {
        return value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }

    private static ReviewSummaryResultDto? ParseReviewAiResponse(string responseText)
    {
        try
        {
            string cleanText = CleanJsonResponse(responseText);

            JsonSerializerOptions serializeOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ReviewSummaryResultDto>(cleanText, serializeOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse AI review response. Raw response: {responseText}", ex);
        }
    }

}
