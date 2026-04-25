using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using TradingJournal.Modules.Trades.Dto;
using TradingJournal.Modules.Trades.Extensions;
using TradingJournal.Modules.Trades.Features.V1.Review;
using TradingJournal.Modules.Trades.Options;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Services;

internal sealed class OpenRouterAiService(
    IPromptService promptService,
    ITradeDbContext context,
    IEmotionTagProvider emotionTagProvider,
    IPsychologyProvider psychologyProvider,
    IReviewSnapshotBuilder reviewSnapshotBuilder,
    HttpClient httpClient,
    IImageHelper imageHelper,
    IOptions<OpenRouterOptions> options,
    IHttpContextAccessor httpContextAccessor) : IOpenRouterAIService
{
    public async Task<TradeAnalysisResultDto?> GenerateTradingOrderSummary(int tradeHistoryId, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetTradingOrderSummary();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Not Found Prompt File.");
        }

        TradeHistory tradeHistory = await LoadTradeHistory(tradeHistoryId, cancellationToken);

        (TradeSummaryDto tradeSummary, List<string> psychologyNotes) = await BuildTradeSummaryDto(tradeHistory, cancellationToken);

        string finalPrompt = BuildPrompt(promptTemplate, tradeSummary, psychologyNotes);

        List<byte[]> imageContents = await imageHelper.GetImageBytesFromUrls(
            tradeHistory.TradeScreenShots.Select(tss => tss.Url).ToList(),
            cancellationToken);

        string responseText = await SendOpenRouterRequest(finalPrompt, imageContents, cancellationToken);

        return ParseAiResponse(responseText);
    }

    private async Task<TradeHistory> LoadTradeHistory(int tradeHistoryId, CancellationToken cancellationToken)
    {
        return await context.TradeHistories
            .AsNoTracking()
            .Include(th => th.TradeScreenShots)
            .Include(th => th.TradeEmotionTags)
            .Include(th => th.TradeChecklists)
                .ThenInclude(th => th.PretradeChecklist)
            .Include(th => th.TradeTechnicalAnalysisTags)
                .ThenInclude(th => th.TechnicalAnalysis)
            .AsSplitQuery()
            .FirstOrDefaultAsync(th => th.Id == tradeHistoryId, cancellationToken)
            ?? throw new InvalidOperationException("Trade history not found.");
    }

    private async Task<(TradeSummaryDto Summary, List<string> PsychologyNotes)> BuildTradeSummaryDto(
        TradeHistory tradeHistory, CancellationToken cancellationToken)
    {
        TradingZone tradingZone = await context.TradingZones
            .FirstOrDefaultAsync(tz => tz.Id == tradeHistory.TradingZoneId, cancellationToken)
            ?? throw new InvalidOperationException("Trading zone not found.");

        List<string> technicalTagNames = [.. tradeHistory.TradeTechnicalAnalysisTags
            .Select(ttat => ttat.TechnicalAnalysis?.Name ?? string.Empty)
            .Where(x => !string.IsNullOrEmpty(x))];

        List<string> tradeEmotionalTags = await GetEmotionTagNames(tradeHistory, cancellationToken);

        List<string> checkListNames = await GetChecklistNames(tradeHistory, cancellationToken);

        List<string> psychologyNotes = await psychologyProvider.GetPsychologyByDate(tradeHistory.Date, cancellationToken);

        TradeSummaryDto summary = new(
            Asset: tradeHistory.Asset,
            EntryPrice: tradeHistory.EntryPrice,
            Position: tradeHistory.Position.ToString(),
            TargetTier1: tradeHistory.TargetTier1,
            TargetTier2: tradeHistory.TargetTier2,
            TargetTier3: tradeHistory.TargetTier3,
            StopLoss: tradeHistory.StopLoss,
            Notes: tradeHistory.Notes ?? string.Empty,
            ExitPrice: tradeHistory.ExitPrice,
            Pnl: tradeHistory.Pnl,
            TradeTechnicalAnalysisTags: technicalTagNames,
            EmotionTags: tradeEmotionalTags,
            ConfidenceLevel: tradeHistory.ConfidenceLevel.ToString(),
            TradeHistoryChecklists: checkListNames,
            TradingZone: tradingZone.Name,
            OpenDate: tradeHistory.Date,
            ClosedDate: tradeHistory.ClosedDate ?? DateTime.Now
        );

        return (summary, psychologyNotes);
    }

    private async Task<List<string>> GetEmotionTagNames(TradeHistory tradeHistory, CancellationToken cancellationToken)
    {
        List<EmotionTagCacheDto> emotionTags = await emotionTagProvider.GetEmotionTagsAsync(cancellationToken);
        HashSet<int> emotionTagIds = [.. tradeHistory.TradeEmotionTags?.Select(tet => tet.EmotionTagId) ?? []];

        return [.. emotionTags.Where(x => emotionTagIds.Contains(x.Id)).Select(x => x.Name)];
    }

    private async Task<List<string>> GetChecklistNames(TradeHistory tradeHistory, CancellationToken cancellationToken)
    {
        HashSet<int> pretradeCheckListIds = [.. tradeHistory.TradeChecklists.Select(x => x.PretradeChecklistId)];

        return await context.PretradeChecklists
            .Where(ptc => pretradeCheckListIds.Contains(ptc.Id))
            .Select(x => x.Name)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    private static string BuildPrompt(string template, TradeSummaryDto summary, List<string> psychologyNotes)
    {
        Dictionary<string, string> replacements = new()
        {
            { "{{Asset}}", summary.Asset },
            { "{{Position}}", summary.Position },
            { "{{EntryPrice}}", summary.EntryPrice.ToString(CultureInfo.InvariantCulture) },
            { "{{TargetTier1}}", summary.TargetTier1.ToString(CultureInfo.InvariantCulture) },
            { "{{TargetTier2}}", summary.TargetTier2?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{TargetTier3}}", summary.TargetTier3?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{StopLoss}}", summary.StopLoss.ToString(CultureInfo.InvariantCulture) },
            { "{{Notes}}", summary.Notes },
            { "{{ExitPrice}}", summary.ExitPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{Pnl}}", summary.Pnl?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{ConfidenceLevel}}", summary.ConfidenceLevel },
            { "{{TradingZone}}", summary.TradingZone },
            { "{{Date}}", summary.OpenDate.ToShortDateString() },
            { "{{ClosedDate}}", summary.ClosedDate.ToShortDateString() },
            { "{{TradeTechnicalAnalysisTags}}", string.Join(", ", summary.TradeTechnicalAnalysisTags ?? []) },
            { "{{TradeHistoryChecklists}}", string.Join(", ", summary.TradeHistoryChecklists) },
            { "{{EmotionTags}}", string.Join(", ", summary.EmotionTags ?? []) },
            { "{{PsychologyNotes}}", string.Join(", ", psychologyNotes) }
        };

        return ReplacePlaceholders(template, replacements);
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, string> replacements)
    {
        StringBuilder sb = new(template);
        foreach (KeyValuePair<string, string> replacement in replacements)
        {
            sb.Replace(replacement.Key, replacement.Value);
        }
        return sb.ToString();
    }

    private async Task<string> SendOpenRouterRequest(
        string prompt,
        List<byte[]> imageContents,
        CancellationToken cancellationToken)
    {
        List<object> allPromptParts =
        [
            new { type = "text", text = prompt }
        ];

        foreach (byte[] content in imageContents)
        {
            allPromptParts.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:image/jpeg;base64,{Convert.ToBase64String(content)}"
                }
            });
        }

        var requestBody = new
        {
            model = options.Value.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = allPromptParts
                }
            }
        };

        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/chat/completions");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        HttpContext? httpContext = httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            request.Headers.Add("HTTP-Referer", $"{httpContext.Request.Scheme}://{httpContext.Request.Host}");
        }
        else
        {
            request.Headers.Add("HTTP-Referer", "http://localhost:3000");
        }
        request.Headers.Add("X-Title", "TradingJournal");

        string jsonBody = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenRouter API failed with status {response.StatusCode}: {errorContent}");
        }

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        using JsonDocument doc = JsonDocument.Parse(responseContent);
        JsonElement root = doc.RootElement;
        
        if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
        {
            JsonElement message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out JsonElement textContent))
            {
                return textContent.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("OpenRouter returned an empty or invalid response.");
    }

    private static TradeAnalysisResultDto? ParseAiResponse(string responseText)
    {
        try
        {
            string cleanText = responseText.Trim();
            if (cleanText.StartsWith("```json"))
            {
                cleanText = cleanText.Substring(7);
            }
            if (cleanText.StartsWith("```"))
            {
                cleanText = cleanText.Substring(3);
            }
            if (cleanText.EndsWith("```"))
            {
                cleanText = cleanText.Substring(0, cleanText.Length - 3);
            }

            JsonSerializerOptions serializeOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<TradeAnalysisResultDto>(cleanText.Trim(), serializeOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse AI response into TradeAnalysisResult. Raw response: {responseText}", ex);
        }
    }

    public async Task<ReviewSummaryResultDto?> GenerateReviewSummary(ReviewSummaryRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetReviewSummary();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Not Found Review Prompt File.");
        }

        ReviewSnapshot snapshot = await reviewSnapshotBuilder.BuildAsync(
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

        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

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

        AppendTradeSection(
            sections,
            "Best trades",
            trades.Where(trade => trade.Pnl > 0)
                .OrderByDescending(trade => trade.Pnl)
                .Take(3));

        AppendTradeSection(
            sections,
            "Worst trades",
            trades.Where(trade => trade.Pnl <= 0)
                .OrderBy(trade => trade.Pnl)
                .Take(3));

        AppendTradeSection(
            sections,
            "Rule-break trades",
            trades.Where(trade => trade.IsRuleBroken)
                .OrderByDescending(trade => Math.Abs(trade.Pnl))
                .Take(3));

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
            string cleanText = responseText.Trim();
            if (cleanText.StartsWith("```json"))
            {
                cleanText = cleanText.Substring(7);
            }
            if (cleanText.StartsWith("```"))
            {
                cleanText = cleanText.Substring(3);
            }
            if (cleanText.EndsWith("```"))
            {
                cleanText = cleanText.Substring(0, cleanText.Length - 3);
            }

            JsonSerializerOptions serializeOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ReviewSummaryResultDto>(cleanText.Trim(), serializeOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse AI review response. Raw response: {responseText}", ex);
        }
    }
    public async Task<AiCoachResponseDto> ChatWithCoachAsync(AiCoachRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetAiCoach();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("AI Coach prompt template not found.");
        }

        // Build trader context from real data
        ReviewSnapshot snapshot = await reviewSnapshotBuilder.BuildAsync(
            ReviewPeriodType.Monthly,
            DateTime.UtcNow.AddDays(-30),
            request.UserId,
            cancellationToken);
        ReviewSnapshotMetrics metrics = snapshot.Metrics;

        // Build recent trades summary
        string recentTrades = snapshot.Trades.Count > 0
            ? string.Join("\n", snapshot.Trades
                .OrderByDescending(t => t.ClosedDate)
                .Take(10)
                .Select(BuildTradeLine))
            : "No closed trades in the last 30 days.";

        // Build psychology notes
        string recentPsychNotes = snapshot.PsychologyNotes.Count > 0
            ? string.Join("\n", snapshot.PsychologyNotes.Take(5))
            : "No psychology notes in the last 30 days.";

        // Fill the system prompt with real data
        Dictionary<string, string> replacements = new()
        {
            { "{{TotalPnl}}", FormatPromptNumber(metrics.TotalPnl, 2) },
            { "{{WinRate}}", FormatPromptNumber(metrics.WinRate, 1) },
            { "{{TotalTrades}}", metrics.TotalTrades.ToString() },
            { "{{Wins}}", metrics.Wins.ToString() },
            { "{{Losses}}", metrics.Losses.ToString() },
            { "{{AverageWin}}", FormatPromptNumber(metrics.AverageWin, 2) },
            { "{{AverageLoss}}", FormatPromptNumber(metrics.AverageLoss, 2) },
            { "{{BestTradePnl}}", FormatPromptNumber(metrics.BestTradePnl, 2) },
            { "{{WorstTradePnl}}", FormatPromptNumber(metrics.WorstTradePnl, 2) },
            { "{{ProfitFactor}}", metrics.AverageLoss != 0
                ? FormatPromptNumber(Math.Abs(metrics.AverageWin * metrics.Wins / (metrics.AverageLoss * metrics.Losses)), 2)
                : "N/A" },
            { "{{MaxDrawdown}}", FormatPromptNumber(metrics.WorstDayPnl, 2) },
            { "{{MaxDrawdownPct}}", "N/A" },
            { "{{SharpeRatio}}", "N/A" },
            { "{{AvgRiskReward}}", "N/A" },
            { "{{ConsecutiveWins}}", metrics.HighConfidenceTrades.ToString() },
            { "{{ConsecutiveLosses}}", metrics.RuleBreakTrades.ToString() },
            { "{{DominantEmotion}}", metrics.DominantEmotion ?? "Unknown" },
            { "{{AvgConfidence}}", "N/A" },
            { "{{PsychologyScore}}", "N/A" },
            { "{{JournalEntryCount}}", snapshot.PsychologyNotes.Count.ToString() },
            { "{{RecentPsychologyNotes}}", recentPsychNotes },
            { "{{RecentTrades}}", recentTrades },
        };

        string systemPrompt = ReplacePlaceholders(promptTemplate, replacements);

        string reply = await SendCoachRequest(systemPrompt, request.Messages, cancellationToken);

        return new AiCoachResponseDto(reply);
    }

    private async Task<string> SendCoachRequest(
        string systemPrompt,
        List<AiCoachMessageDto> conversationHistory,
        CancellationToken cancellationToken)
    {
        List<object> messages =
        [
            new { role = "system", content = systemPrompt }
        ];

        foreach (AiCoachMessageDto message in conversationHistory)
        {
            messages.Add(new { role = message.Role, content = message.Content });
        }

        var requestBody = new
        {
            model = options.Value.Model,
            messages,
            max_tokens = 1024,
            temperature = 0.7
        };

        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/chat/completions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        HttpContext? httpContext = httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            request.Headers.Add("HTTP-Referer", $"{httpContext.Request.Scheme}://{httpContext.Request.Host}");
        }
        else
        {
            request.Headers.Add("HTTP-Referer", "http://localhost:3000");
        }
        request.Headers.Add("X-Title", "TradingJournal");

        string jsonBody = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenRouter AI Coach failed with status {response.StatusCode}: {errorContent}");
        }

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        using JsonDocument doc = JsonDocument.Parse(responseContent);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
        {
            JsonElement message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out JsonElement textContent))
            {
                return textContent.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("OpenRouter returned an empty or invalid response for AI Coach.");
    }
}

