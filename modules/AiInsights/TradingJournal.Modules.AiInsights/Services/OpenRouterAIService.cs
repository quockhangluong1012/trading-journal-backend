using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Extensions;
using TradingJournal.Modules.AiInsights.Options;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.AiInsights.Services;

internal sealed class OpenRouterAiService(
    IPromptService promptService,
    IAiTradeDataProvider tradeDataProvider,
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

        AiTradeDetailDto tradeDetail = await tradeDataProvider.GetTradeDetailForAiAsync(tradeHistoryId, cancellationToken);

        string finalPrompt = BuildPrompt(promptTemplate, tradeDetail);

        List<byte[]> imageContents = await imageHelper.GetImageBytesFromUrls(
            tradeDetail.ScreenshotUrls,
            cancellationToken);

        string responseText = await SendOpenRouterRequest(finalPrompt, imageContents, cancellationToken);

        return ParseAiResponse(responseText);
    }

    private static string BuildPrompt(string template, AiTradeDetailDto detail)
    {
        Dictionary<string, string> replacements = new()
        {
            { "{{Asset}}", detail.Asset },
            { "{{Position}}", detail.Position },
            { "{{EntryPrice}}", detail.EntryPrice.ToString(CultureInfo.InvariantCulture) },
            { "{{TargetTier1}}", detail.TargetTier1.ToString(CultureInfo.InvariantCulture) },
            { "{{TargetTier2}}", detail.TargetTier2?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{TargetTier3}}", detail.TargetTier3?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{StopLoss}}", detail.StopLoss.ToString(CultureInfo.InvariantCulture) },
            { "{{Notes}}", detail.Notes },
            { "{{ExitPrice}}", detail.ExitPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{Pnl}}", detail.Pnl?.ToString(CultureInfo.InvariantCulture) ?? string.Empty },
            { "{{ConfidenceLevel}}", detail.ConfidenceLevel },
            { "{{TradingZone}}", detail.TradingZone },
            { "{{Date}}", detail.OpenDate.ToShortDateString() },
            { "{{ClosedDate}}", detail.ClosedDate.ToShortDateString() },
            { "{{TradeTechnicalAnalysisTags}}", string.Join(", ", detail.TechnicalAnalysisTags) },
            { "{{TradeHistoryChecklists}}", string.Join(", ", detail.ChecklistItems) },
            { "{{EmotionTags}}", string.Join(", ", detail.EmotionTags) },
            { "{{PsychologyNotes}}", string.Join(", ", detail.PsychologyNotes) }
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

    private Task<string> SendOpenRouterRequest(
        string prompt,
        List<byte[]> imageContents,
        CancellationToken cancellationToken)
    {
        List<object> userContentParts = [new { type = "text", text = prompt }];

        foreach (byte[] content in imageContents)
        {
            userContentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/jpeg;base64,{Convert.ToBase64String(content)}" }
            });
        }

        List<object> messages = [new { role = "user", content = (object)userContentParts }];
        return SendChatCompletionAsync(messages, cancellationToken: cancellationToken);
    }

    private static TradeAnalysisResultDto? ParseAiResponse(string responseText)
    {
        try
        {
            string cleanText = CleanJsonResponse(responseText);

            JsonSerializerOptions serializeOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<TradeAnalysisResultDto>(cleanText, serializeOptions);
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

    public async Task<AiCoachResponseDto> ChatWithCoachAsync(AiCoachRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetAiCoach();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("AI Coach prompt template not found.");
        }

        ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
            ReviewPeriodType.Monthly,
            DateTime.UtcNow.AddDays(-30),
            request.UserId,
            cancellationToken);
        ReviewSnapshotMetrics metrics = snapshot.Metrics;

        string recentTrades = snapshot.Trades.Count > 0
            ? string.Join("\n", snapshot.Trades
                .OrderByDescending(t => t.ClosedDate)
                .Take(10)
                .Select(BuildTradeLine))
            : "No closed trades in the last 30 days.";

        string recentPsychNotes = snapshot.PsychologyNotes.Count > 0
            ? string.Join("\n", snapshot.PsychologyNotes.Take(5))
            : "No psychology notes in the last 30 days.";

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

    private Task<string> SendCoachRequest(
        string systemPrompt,
        List<AiCoachMessageDto> conversationHistory,
        CancellationToken cancellationToken)
    {
        List<object> messages = [new { role = "system", content = (object)systemPrompt }];

        foreach (AiCoachMessageDto msg in conversationHistory)
        {
            messages.Add(new { role = msg.Role, content = (object)msg.Content });
        }

        return SendChatCompletionAsync(messages, maxTokens: 1024, temperature: 0.7, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Shared HTTP request method for all OpenRouter chat completions.
    /// Eliminates duplicated auth, header, serialization, and response parsing logic.
    /// </summary>
    private async Task<string> SendChatCompletionAsync(
        List<object> messages,
        int? maxTokens = null,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = options.Value.Model,
            ["messages"] = messages
        };

        if (maxTokens.HasValue) requestBody["max_tokens"] = maxTokens.Value;
        if (temperature.HasValue) requestBody["temperature"] = temperature.Value;

        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string referer = httpContext is not null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : "http://localhost:3000";
        request.Headers.Add("HTTP-Referer", referer);
        request.Headers.Add("X-Title", "TradingJournal");

        string jsonBody = JsonSerializer.Serialize(requestBody);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"OpenRouter API failed with status {response.StatusCode}: {errorContent}");
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

    public async Task<PreTradeValidationResultDto?> ValidateTradeSetupAsync(
        PreTradeValidationRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetPreTradeValidation();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Pre-Trade Validation prompt template not found.");
        }

        // Build recent performance context
        string recentPerformance = "No recent performance data available.";
        try
        {
            ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
                ReviewPeriodType.Monthly,
                DateTime.UtcNow.AddDays(-30),
                request.UserId,
                cancellationToken);
            ReviewSnapshotMetrics metrics = snapshot.Metrics;

            recentPerformance = $"Last 30 days: {metrics.TotalTrades} trades, " +
                $"Win Rate: {FormatPromptNumber(metrics.WinRate, 1)}%, " +
                $"P&L: {FormatPromptNumber(metrics.TotalPnl, 2)}, " +
                $"Wins: {metrics.Wins}, Losses: {metrics.Losses}, " +
                $"Avg Win: {FormatPromptNumber(metrics.AverageWin, 2)}, " +
                $"Avg Loss: {FormatPromptNumber(metrics.AverageLoss, 2)}, " +
                $"Rule Breaks: {metrics.RuleBreakTrades}";
        }
        catch
        {
            // Gracefully continue without performance data
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{Asset}}", request.Asset },
            { "{{Position}}", request.Position },
            { "{{EntryPrice}}", FormatPromptNumber(request.EntryPrice, 5) },
            { "{{StopLoss}}", FormatPromptNumber(request.StopLoss, 5) },
            { "{{TargetTier1}}", FormatPromptNumber(request.TargetTier1, 5) },
            { "{{TargetTier2}}", request.TargetTier2?.ToString(CultureInfo.InvariantCulture) ?? "Not set" },
            { "{{TargetTier3}}", request.TargetTier3?.ToString(CultureInfo.InvariantCulture) ?? "Not set" },
            { "{{ConfidenceLevel}}", request.ConfidenceLevel.ToString() },
            { "{{TradingZone}}", request.TradingZone ?? "Not specified" },
            { "{{TechnicalAnalysisTags}}", request.TechnicalAnalysisTags is { Count: > 0 } ? string.Join(", ", request.TechnicalAnalysisTags) : "None" },
            { "{{ChecklistStatus}}", request.ChecklistStatus ?? "Not completed" },
            { "{{EmotionTags}}", request.EmotionTags is { Count: > 0 } ? string.Join(", ", request.EmotionTags) : "None" },
            { "{{Notes}}", request.Notes ?? "No notes provided" },
            { "{{RecentPerformance}}", recentPerformance },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        return ParseJsonResponse<PreTradeValidationResultDto>(responseText);
    }

    public async Task<EmotionDetectionResultDto?> DetectEmotionsAsync(
        EmotionDetectionRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetEmotionDetection();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Emotion Detection prompt template not found.");
        }

        // Fetch available emotions for the user
        string availableEmotions = "No emotion tags configured in the system.";
        try
        {
            // We use the httpClient's base address context — emotions come from a different module,
            // so we pass the list as a prompt parameter instead of cross-module dependency
            availableEmotions = "Focused, Calm, Confident, Anxious, Fearful, Greedy, " +
                "Frustrated, Impatient, Euphoric, Hesitant, Disciplined, Revenge, " +
                "FOMO, Overconfident, Bored, Tired, Stressed, Hopeful, Doubtful, Neutral";
        }
        catch
        {
            // Use defaults
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{AvailableEmotions}}", availableEmotions },
            { "{{TextContent}}", request.TextContent },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        return ParseJsonResponse<EmotionDetectionResultDto>(responseText);
    }

    public async Task<MorningBriefingResultDto?> GenerateMorningBriefingAsync(
        MorningBriefingRequestDto request, CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetMorningBriefing();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Morning Briefing prompt template not found.");
        }

        // Build context from recent performance
        ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
            ReviewPeriodType.Monthly,
            DateTime.UtcNow.AddDays(-30),
            request.UserId,
            cancellationToken);
        ReviewSnapshotMetrics metrics = snapshot.Metrics;

        // Build open positions summary
        string openPositions = "No open positions.";
        // Streak description
        string streakDescription = "No streak data available.";

        // Recent trades to detect streak
        if (snapshot.Trades.Count > 0)
        {
            List<ReviewTradeInsight> recentTrades = [.. snapshot.Trades
                .OrderByDescending(t => t.ClosedDate)
                .Take(5)];

            int winStreak = 0;
            int lossStreak = 0;
            foreach (ReviewTradeInsight trade in recentTrades)
            {
                if (trade.Pnl > 0) { winStreak++; lossStreak = 0; }
                else { lossStreak++; winStreak = 0; }
                if (winStreak == 0 && lossStreak == 0) break;
            }

            streakDescription = winStreak > 0
                ? $"{winStreak}-trade win streak"
                : lossStreak > 0
                    ? $"{lossStreak}-trade loss streak"
                    : "Mixed results recently";
        }

        // Psychology notes
        string recentPsychNotes = snapshot.PsychologyNotes.Count > 0
            ? string.Join("\n", snapshot.PsychologyNotes.Take(3))
            : "No recent psychology notes.";

        Dictionary<string, string> replacements = new()
        {
            { "{{TotalPnl}}", FormatPromptNumber(metrics.TotalPnl, 2) },
            { "{{WinRate}}", FormatPromptNumber(metrics.WinRate, 1) },
            { "{{TotalTrades}}", metrics.TotalTrades.ToString() },
            { "{{Wins}}", metrics.Wins.ToString() },
            { "{{Losses}}", metrics.Losses.ToString() },
            { "{{StreakDescription}}", streakDescription },
            { "{{OpenPositions}}", openPositions },
            { "{{TiltScore}}", "Not available" },
            { "{{YesterdayNote}}", "No daily note from yesterday." },
            { "{{EconomicEvents}}", "No economic event data available." },
            { "{{RecentPsychologyNotes}}", recentPsychNotes },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        return ParseJsonResponse<MorningBriefingResultDto>(responseText);
    }

    private static T? ParseJsonResponse<T>(string responseText)
    {
        try
        {
            string cleanText = CleanJsonResponse(responseText);

            JsonSerializerOptions serializeOptions = new()
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<T>(cleanText, serializeOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse AI response into {typeof(T).Name}. Raw response: {responseText}", ex);
        }
    }

    private static string CleanJsonResponse(string responseText)
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
        return cleanText.Trim();
    }
}
