using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Extensions;
using TradingJournal.Modules.AiInsights.Options;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.AiInsights.Services;

internal sealed class OpenRouterAiService(
    IPromptService promptService,
    IAiTradeDataProvider tradeDataProvider,
    ITradeAiContextService tradeAiContextService,
    IEconomicImpactContextProvider economicImpactContextProvider,
    IRiskContextProvider riskContextProvider,
    ITradeProvider tradeProvider,
    IChecklistModelProvider checklistModelProvider,
    ISetupProvider setupProvider,
    HttpClient httpClient,
    IImageHelper imageHelper,
    IOptions<OpenRouterOptions> options,
    IHttpContextAccessor httpContextAccessor) : IOpenRouterAIService
{
    private const int MaxChartAnalysisImages = 3;
    private const int MaxInlineImageBytes = 5 * 1024 * 1024;
    private static readonly Regex PromptCodeFencePattern = new("```.*?```", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex PromptRolePrefixPattern = new(@"(?im)^\s*(system|assistant|developer|user)\s*:\s*", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedSetupNodeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "start",
        "step",
        "decision",
        "end"
    };
    private static readonly string[] SupportedInlineImagePrefixes =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/jpg;base64,",
        "data:image/webp;base64,"
    ];

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

    public async Task<PreTradeChecklistInterpretationResultDto?> InterpretPreTradeChecklistAsync(
        PreTradeChecklistInterpretationRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetChecklistInterpretation();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Checklist interpretation prompt template not found.");
        }

        ChecklistModelContextDto? checklistModel = await checklistModelProvider.GetChecklistModelAsync(
            request.UserId,
            request.ChecklistModelId,
            cancellationToken);

        if (checklistModel is null)
        {
            throw new InvalidOperationException("Checklist model not found for the current user.");
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{ChecklistModelId}}", checklistModel.Id.ToString(CultureInfo.InvariantCulture) },
            { "{{ChecklistModelName}}", checklistModel.Name },
            { "{{ChecklistModelDescription}}", string.IsNullOrWhiteSpace(checklistModel.Description) ? "No description provided." : checklistModel.Description },
            { "{{ChecklistCriteria}}", BuildChecklistCriteriaDigest(checklistModel.Criteria) },
            { "{{ChecklistInput}}", FormatPromptInputBlock("user_input", request.Input) },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        PreTradeChecklistInterpretationResultDto? response = ParseJsonResponse<PreTradeChecklistInterpretationResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.ChecklistModelId = checklistModel.Id;
        return SanitizeChecklistInterpretationResult(response, checklistModel);
    }

    public async Task<ChartScreenshotAnalysisResultDto?> AnalyzeChartScreenshotAsync(
        ChartScreenshotAnalysisRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetChartScreenshotAnalysis();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Chart screenshot analysis prompt template not found.");
        }

        List<byte[]> imageContents = await LoadImageSourcesAsync(request.Screenshots, cancellationToken);

        if (imageContents.Count == 0)
        {
            throw new InvalidOperationException("No valid screenshots were provided for AI chart analysis.");
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{Asset}}", request.Asset },
            { "{{Position}}", string.IsNullOrWhiteSpace(request.Position) ? "Unspecified" : request.Position },
            { "{{EntryPrice}}", request.EntryPrice.HasValue ? FormatPromptNumber(request.EntryPrice.Value, 5) : "Not specified" },
            { "{{StopLoss}}", request.StopLoss.HasValue ? FormatPromptNumber(request.StopLoss.Value, 5) : "Not specified" },
            { "{{TargetTier1}}", request.TargetTier1.HasValue ? FormatPromptNumber(request.TargetTier1.Value, 5) : "Not specified" },
            { "{{TradingZone}}", request.TradingZone ?? "Not specified" },
            { "{{Notes}}", string.IsNullOrWhiteSpace(request.Notes) ? "No notes provided." : request.Notes },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, imageContents, cancellationToken);
        ChartScreenshotAnalysisResultDto? response = ParseJsonResponse<ChartScreenshotAnalysisResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.ConfidenceScore = Math.Clamp(response.ConfidenceScore, 0m, 1m);
        response.KeyLevels = SanitizeStringList(response.KeyLevels);
        response.DetectedConfluences = SanitizeStringList(response.DetectedConfluences);
        response.Warnings = SanitizeStringList(response.Warnings);
        response.SuggestedActions = SanitizeStringList(response.SuggestedActions);

        return response;
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

    public async Task<AiRiskAdvisorResultDto?> GenerateRiskAdvisorAsync(
        AiRiskAdvisorRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetRiskAdvisor();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Risk advisor prompt template not found.");
        }

        RiskAdvisorContextDto riskContext = await riskContextProvider.GetRiskContextAsync(
            request.UserId,
            cancellationToken);
        TradeAiContextSnapshot recentTrades = await tradeAiContextService.BuildRecentClosedTradesContextAsync(
            request.UserId,
            maxTrades: 8,
            cancellationToken);

        Dictionary<string, string> replacements = new()
        {
            { "{{AccountBalance}}", FormatPromptNumber(riskContext.AccountBalance, 2) },
            { "{{DailyLossLimitPercent}}", FormatPromptNumber(riskContext.DailyLossLimitPercent, 2) },
            { "{{WeeklyDrawdownCapPercent}}", FormatPromptNumber(riskContext.WeeklyDrawdownCapPercent, 2) },
            { "{{MaxOpenPositions}}", riskContext.MaxOpenPositions.ToString(CultureInfo.InvariantCulture) },
            { "{{DailyPnl}}", FormatPromptNumber(riskContext.DailyPnl, 2) },
            { "{{DailyPnlPercent}}", FormatPromptNumber(riskContext.DailyPnlPercent, 2) },
            { "{{WeeklyPnl}}", FormatPromptNumber(riskContext.WeeklyPnl, 2) },
            { "{{WeeklyPnlPercent}}", FormatPromptNumber(riskContext.WeeklyPnlPercent, 2) },
            { "{{TodayTradeCount}}", riskContext.TodayTradeCount.ToString(CultureInfo.InvariantCulture) },
            { "{{OpenPositionCount}}", riskContext.OpenPositionCount.ToString(CultureInfo.InvariantCulture) },
            { "{{WeekTradeCount}}", riskContext.WeekTradeCount.ToString(CultureInfo.InvariantCulture) },
            { "{{TodayWins}}", riskContext.TodayWins.ToString(CultureInfo.InvariantCulture) },
            { "{{TodayLosses}}", riskContext.TodayLosses.ToString(CultureInfo.InvariantCulture) },
            { "{{DailyLimitUsedPercent}}", FormatPromptNumber(riskContext.DailyLimitUsedPercent, 2) },
            { "{{WeeklyCapUsedPercent}}", FormatPromptNumber(riskContext.WeeklyCapUsedPercent, 2) },
            { "{{IsDailyLimitBreached}}", riskContext.IsDailyLimitBreached ? "Yes" : "No" },
            { "{{IsWeeklyCapBreached}}", riskContext.IsWeeklyCapBreached ? "Yes" : "No" },
            { "{{RiskAlerts}}", BuildRiskAlertDigest(riskContext.Alerts) },
            { "{{RecentTrades}}", recentTrades.TradeDigest },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        AiRiskAdvisorResultDto? response = ParseJsonResponse<AiRiskAdvisorResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.RiskLevel = SanitizeRiskLevel(response.RiskLevel ?? string.Empty);
        response.Summary = response.Summary?.Trim() ?? string.Empty;
        response.PositionSizingAdvice = response.PositionSizingAdvice?.Trim() ?? string.Empty;
        response.KeyRisks = SanitizeStringList(response.KeyRisks);
        response.ActionItems = SanitizeStringList(response.ActionItems);
        response.Confidence = Math.Clamp(response.Confidence, 0m, 1m);

        return response;
    }

    public async Task<AiWeeklyDigestResultDto?> GenerateWeeklyDigestAsync(
        AiWeeklyDigestRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetWeeklyDigest();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Weekly digest prompt template not found.");
        }

        ReviewSnapshot snapshot = await tradeDataProvider.BuildReviewSnapshotAsync(
            ReviewPeriodType.Weekly,
            request.ReferenceDate,
            request.UserId,
            cancellationToken);
        ReviewSnapshotMetrics metrics = snapshot.Metrics;

        Dictionary<string, string> replacements = new()
        {
            { "{{PeriodStart}}", FormatPromptDate(snapshot.PeriodStart) },
            { "{{PeriodEnd}}", FormatPromptDate(snapshot.PeriodEnd) },
            { "{{TotalPnl}}", FormatPromptNumber(metrics.TotalPnl, 2) },
            { "{{WinRate}}", FormatPromptNumber(metrics.WinRate, 1) },
            { "{{TotalTrades}}", metrics.TotalTrades.ToString(CultureInfo.InvariantCulture) },
            { "{{Wins}}", metrics.Wins.ToString(CultureInfo.InvariantCulture) },
            { "{{Losses}}", metrics.Losses.ToString(CultureInfo.InvariantCulture) },
            { "{{AverageWin}}", FormatPromptNumber(metrics.AverageWin, 2) },
            { "{{AverageLoss}}", FormatPromptNumber(metrics.AverageLoss, 2) },
            { "{{RuleBreakTrades}}", metrics.RuleBreakTrades.ToString(CultureInfo.InvariantCulture) },
            { "{{HighConfidenceTrades}}", metrics.HighConfidenceTrades.ToString(CultureInfo.InvariantCulture) },
            { "{{TopAsset}}", metrics.TopAsset ?? "No data available" },
            { "{{PrimaryTradingZone}}", metrics.PrimaryTradingZone ?? "No data available" },
            { "{{DominantEmotion}}", metrics.DominantEmotion ?? "No data available" },
            { "{{TopTechnicalTheme}}", metrics.TopTechnicalTheme ?? "No data available" },
            { "{{TradeCaseStudies}}", BuildReviewTradeCaseStudies(snapshot.Trades) },
            { "{{TradesList}}", BuildReviewTradeList(snapshot.Trades) },
            { "{{PsychologyNotes}}", BuildPsychologyDigest(snapshot.PsychologyNotes) },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        AiWeeklyDigestResultDto? response = ParseJsonResponse<AiWeeklyDigestResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.Headline = response.Headline?.Trim() ?? string.Empty;
        response.Summary = response.Summary?.Trim() ?? string.Empty;
        response.FocusForNextWeek = response.FocusForNextWeek?.Trim() ?? string.Empty;
        response.KeyWins = SanitizeStringList(response.KeyWins);
        response.KeyRisks = SanitizeStringList(response.KeyRisks);
        response.ActionItems = SanitizeStringList(response.ActionItems);

        return response;
    }

    public async Task<AiEconomicImpactPredictorResultDto?> GenerateEconomicImpactPredictionAsync(
        AiEconomicImpactPredictorRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetEconomicImpactPredictor();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Economic impact predictor prompt template not found.");
        }

        EconomicImpactContextDto context = await economicImpactContextProvider.GetEconomicImpactContextAsync(
            request.UserId,
            request.Symbol,
            request.ProximityMinutes,
            cancellationToken);

        Dictionary<string, string> replacements = new()
        {
            { "{{Symbol}}", context.Symbol },
            { "{{SafetyLevel}}", context.SafetyLevel },
            { "{{SafetyMessage}}", context.SafetyMessage },
            { "{{MinutesUntilNextHighImpactEvent}}", context.MinutesUntilNextHighImpactEvent?.ToString(CultureInfo.InvariantCulture) ?? "None" },
            { "{{RecommendedWaitMinutes}}", context.RecommendedWaitMinutes.ToString(CultureInfo.InvariantCulture) },
            { "{{TradesNearEvents}}", context.TradesNearEvents.ToString(CultureInfo.InvariantCulture) },
            { "{{TradesAwayFromEvents}}", context.TradesAwayFromEvents.ToString(CultureInfo.InvariantCulture) },
            { "{{WinRateNear}}", FormatPromptNumber(context.WinRateNear, 1) },
            { "{{WinRateAway}}", FormatPromptNumber(context.WinRateAway, 1) },
            { "{{AvgPnlNear}}", FormatPromptNumber(context.AvgPnlNear, 2) },
            { "{{AvgPnlAway}}", FormatPromptNumber(context.AvgPnlAway, 2) },
            { "{{CorrelationSummary}}", context.CorrelationSummary },
            { "{{UpcomingEvents}}", BuildEconomicEventDigest(context.UpcomingEvents) },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        AiEconomicImpactPredictorResultDto? response = ParseJsonResponse<AiEconomicImpactPredictorResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.RiskLevel = SanitizeRiskLevel(response.RiskLevel ?? string.Empty);
        response.Summary = response.Summary?.Trim() ?? string.Empty;
        response.TradeStance = response.TradeStance?.Trim() ?? string.Empty;
        response.KeyDrivers = SanitizeStringList(response.KeyDrivers);
        response.ActionItems = SanitizeStringList(response.ActionItems);
        response.Confidence = Math.Clamp(response.Confidence, 0m, 1m);

        return response;
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
        try
        {
            string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
            return ParseJsonResponse<MorningBriefingResultDto>(responseText);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate morning briefing: {ex.Message}", ex);
        }
    }

    public async Task<NaturalLanguageTradeSearchResultDto?> SearchTradesNaturalLanguageAsync(
        NaturalLanguageTradeSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetNaturalLanguageTradeSearch();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Natural language trade search prompt template not found.");
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{CurrentDateUtc}}", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            { "{{UserQuery}}", request.Query.Trim() },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

        return ParseJsonResponse<NaturalLanguageTradeSearchResultDto>(responseText);
    }

    public async Task<TradePatternDiscoveryResultDto?> DiscoverTradePatternsAsync(
        TradePatternDiscoveryRequestDto request,
        CancellationToken cancellationToken)
    {
        TradeAiContextSnapshot context = await tradeAiContextService.BuildPatternContextAsync(
            request.UserId,
            request.FromDate,
            request.ToDate,
            maxTrades: 60,
            cancellationToken);

        if (context.SampleSize == 0)
        {
            return new TradePatternDiscoveryResultDto
            {
                Summary = "No closed trades matched the selected range yet.",
                SampleSize = 0,
                ActionItems = ["Close and journal a few trades in this range to unlock AI pattern mining."],
            };
        }

        string promptTemplate = await promptService.GetTradePatternDiscovery();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Trade pattern discovery prompt template not found.");
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{RangeSummary}}", context.RangeSummary },
            { "{{SampleSize}}", context.SampleSize.ToString(CultureInfo.InvariantCulture) },
            { "{{TradeDigest}}", context.TradeDigest },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

        TradePatternDiscoveryResultDto? response = ParseJsonResponse<TradePatternDiscoveryResultDto>(responseText);

        if (response is not null && response.SampleSize == 0)
        {
            response.SampleSize = context.SampleSize;
        }

        return response;
    }

    public async Task<SuggestedLessonsResultDto?> SuggestLessonsAsync(
        SuggestLessonsRequestDto request,
        CancellationToken cancellationToken)
    {
        LessonSuggestionContextDto context = await tradeDataProvider.GetLessonSuggestionContextAsync(
            request.FromDate,
            request.ToDate,
            request.UserId,
            maxTrades: 40,
            cancellationToken);

        if (context.SampleSize == 0)
        {
            return new SuggestedLessonsResultDto
            {
                Summary = "No closed trades matched the selected range yet.",
                SampleSize = 0,
                Suggestions = [],
            };
        }

        string promptTemplate = await promptService.GetSuggestedLessons();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Suggested lessons prompt template not found.");
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{RangeSummary}}", context.RangeSummary },
            { "{{SampleSize}}", context.SampleSize.ToString(CultureInfo.InvariantCulture) },
            { "{{ExistingLessons}}", BuildExistingLessonDigest(context.ExistingLessons) },
            { "{{TradeDigest}}", BuildLessonSuggestionTradeDigest(context.Trades) },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

        SuggestedLessonsResultDto? response = ParseJsonResponse<SuggestedLessonsResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.SampleSize = context.SampleSize;
        response.Suggestions = SanitizeSuggestedLessons(response.Suggestions, context);

        return response;
    }

    public async Task<PlaybookOptimizationResultDto?> OptimizePlaybookAsync(
        PlaybookOptimizationRequestDto request,
        CancellationToken cancellationToken)
    {
        DateTime? start = request.FromDate?.Date;
        DateTime? end = request.ToDate?.Date.AddDays(1).AddTicks(-1);

        List<TradeCacheDto> trades = await tradeProvider.GetTradesAsync(request.UserId, cancellationToken);
        List<SetupSummaryDto> setups = await setupProvider.GetSetupsAsync(request.UserId, cancellationToken);

        List<TradeCacheDto> closedTrades = [.. trades
            .Where(trade => trade.Status == Shared.Common.Enum.TradeStatus.Closed && trade.Pnl.HasValue && trade.TradingSetupId.HasValue)
            .Where(trade => !start.HasValue || (trade.ClosedDate.HasValue && trade.ClosedDate.Value >= start.Value))
            .Where(trade => !end.HasValue || (trade.ClosedDate.HasValue && trade.ClosedDate.Value <= end.Value))];

        if (closedTrades.Count == 0 || setups.Count == 0)
        {
            return new PlaybookOptimizationResultDto
            {
                Summary = "No playbook setup data matched the selected range yet.",
                SampleSize = 0,
                Recommendations = [],
            };
        }

        List<PlaybookSetupSnapshot> playbookSnapshots = [.. setups
            .Where(setup => setup.Status != 4)
            .Select(setup => BuildPlaybookSetupSnapshot(setup, closedTrades))
            .OrderByDescending(snapshot => snapshot.TotalTrades)
            .ThenByDescending(snapshot => snapshot.TotalPnl)];

        if (playbookSnapshots.Count == 0)
        {
            return new PlaybookOptimizationResultDto
            {
                Summary = "No active playbook setups are available to optimize.",
                SampleSize = 0,
                Recommendations = [],
            };
        }

        string promptTemplate = await promptService.GetPlaybookOptimization();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Playbook optimization prompt template not found.");
        }

        Dictionary<string, string> replacements = new()
        {
            { "{{RangeSummary}}", BuildDateRangeSummary(start, end) },
            { "{{SampleSize}}", playbookSnapshots.Count.ToString(CultureInfo.InvariantCulture) },
            { "{{SetupDigest}}", BuildPlaybookSetupDigest(playbookSnapshots) },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

        PlaybookOptimizationResultDto? response = ParseJsonResponse<PlaybookOptimizationResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.SampleSize = playbookSnapshots.Count;
        response.Recommendations = EnrichPlaybookRecommendations(response.Recommendations, playbookSnapshots);

        return response;
    }

    public async Task<TradingSetupGenerationResultDto?> GenerateTradingSetupAsync(
        TradingSetupGenerationRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetTradingSetupGeneration();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Trading setup generation prompt template not found.");
        }

        List<SetupSummaryDto> setups = request.DedupeAgainstExisting
            ? await setupProvider.GetSetupsAsync(request.UserId, cancellationToken)
            : [];

        Dictionary<string, string> replacements = new()
        {
            { "{{UserPrompt}}", FormatPromptInputBlock("user_request", request.Prompt) },
            { "{{MaxNodes}}", request.MaxNodes.ToString(CultureInfo.InvariantCulture) },
            { "{{ExistingSetups}}", BuildExistingSetupsDigest(setups) },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);
        TradingSetupGenerationResultDto? response = ParseJsonResponse<TradingSetupGenerationResultDto>(responseText);

        return response is null ? null : SanitizeTradingSetupGenerationResult(response, request.MaxNodes);
    }

    public async Task<AiTiltInterventionResultDto?> AnalyzeTiltInterventionAsync(
        AiTiltInterventionRequestDto request,
        CancellationToken cancellationToken)
    {
        string promptTemplate = await promptService.GetTiltIntervention();

        if (string.IsNullOrEmpty(promptTemplate))
        {
            throw new InvalidOperationException("Tilt intervention prompt template not found.");
        }

        TradeAiContextSnapshot context = await tradeAiContextService.BuildRecentClosedTradesContextAsync(
            request.UserId,
            maxTrades: 10,
            cancellationToken);

        Dictionary<string, string> replacements = new()
        {
            { "{{TiltScore}}", request.TiltScore.ToString(CultureInfo.InvariantCulture) },
            { "{{TiltLevel}}", request.TiltLevel },
            { "{{ConsecutiveLosses}}", request.ConsecutiveLosses.ToString(CultureInfo.InvariantCulture) },
            { "{{TradesLastHour}}", request.TradesLastHour.ToString(CultureInfo.InvariantCulture) },
            { "{{RuleBreaksToday}}", request.RuleBreaksToday.ToString(CultureInfo.InvariantCulture) },
            { "{{TodayPnl}}", request.TodayPnl.ToString("F2", CultureInfo.InvariantCulture) },
            { "{{CooldownUntil}}", request.CooldownUntil?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "No cooldown active" },
            { "{{RecentTrades}}", context.TradeDigest },
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

        AiTiltInterventionResultDto? response = ParseJsonResponse<AiTiltInterventionResultDto>(responseText);

        if (response is null)
        {
            return null;
        }

        response.RiskLevel = SanitizeRiskLevel(response.RiskLevel ?? string.Empty);
        response.TiltType = response.TiltType?.Trim() ?? "discipline";
        response.Title = response.Title?.Trim() ?? string.Empty;
        response.Message = response.Message?.Trim() ?? string.Empty;
        response.ActionItems = SanitizeStringList(response.ActionItems);
        response.ShouldNotify = response.ShouldNotify
            && !string.IsNullOrWhiteSpace(response.Title)
            && !string.IsNullOrWhiteSpace(response.Message);

        return response;
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
            throw new InvalidOperationException($"Failed to parse AI response into {typeof(T).Name}.", ex);
        }
    }

    private static string FormatPromptInputBlock(string tagName, string input)
    {
        string sanitizedInput = SanitizePromptInput(input);
        return $"<{tagName}>\n{sanitizedInput}\n</{tagName}>";
    }

    private static string SanitizePromptInput(string input)
    {
        string sanitized = PromptCodeFencePattern.Replace(input, " ");
        sanitized = PromptRolePrefixPattern.Replace(sanitized, string.Empty);
        sanitized = sanitized.Replace("\0", string.Empty, StringComparison.Ordinal);
        sanitized = sanitized.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "No additional notes provided." : sanitized;
    }

    private static string BuildLessonSuggestionTradeDigest(IReadOnlyList<LessonSuggestionTradeDto> trades)
    {
        if (trades.Count == 0)
        {
            return "No closed trades matched the requested range.";
        }

        return string.Join("\n", trades.Select(trade =>
            $"- {FormatPromptDate(trade.ClosedDate)} | TradeId: {trade.TradeId} | {trade.Asset} | {trade.Position} | PnL: {FormatPromptNumber(trade.Pnl, 2)} | Zone: {trade.TradingZone} | RuleBroken: {(trade.IsRuleBroken ? "Yes" : "No")} | Technical: {JoinOrFallback(trade.TechnicalThemes)} | Emotions: {JoinOrFallback(trade.EmotionTags)} | Notes: {(string.IsNullOrWhiteSpace(trade.Notes) ? "No note" : trade.Notes)}"));
    }

    private static string BuildExistingLessonDigest(IReadOnlyList<ExistingLessonContextDto> lessons)
    {
        if (lessons.Count == 0)
        {
            return "No existing lessons yet.";
        }

        return string.Join("\n", lessons.Select(lesson =>
            $"- {lesson.Title} | Category: {lesson.Category} | Linked trades: {(lesson.LinkedTradeIds.Count > 0 ? string.Join(", ", lesson.LinkedTradeIds) : "None")} | Takeaway: {lesson.KeyTakeaway ?? "None"}"));
    }

    private static string BuildPlaybookSetupDigest(IReadOnlyList<PlaybookSetupSnapshot> playbookSnapshots)
    {
        return string.Join("\n", playbookSnapshots.Select(snapshot =>
            $"- SetupId: {snapshot.SetupId} | Name: {snapshot.SetupName} | Description: {snapshot.Description ?? "None"} | Status: {snapshot.Status} | Trades: {snapshot.TotalTrades} | Wins: {snapshot.Wins} | Losses: {snapshot.Losses} | WinRate: {FormatPromptNumber(snapshot.WinRate, 1)} | TotalPnL: {FormatPromptNumber(snapshot.TotalPnl, 2)} | ProfitFactor: {FormatPromptMetric(snapshot.ProfitFactor)} | Expectancy: {FormatPromptNumber(snapshot.Expectancy, 2)} | AvgRR: {FormatPromptNumber(snapshot.AvgRiskReward, 2)} | Grade: {snapshot.Grade}"));
    }

    private static string BuildChecklistCriteriaDigest(IReadOnlyCollection<ChecklistCriterionContextDto> criteria)
    {
        if (criteria.Count == 0)
        {
            return "No checklist criteria found.";
        }

        return string.Join("\n", criteria.Select(criterion =>
            $"- Id: {criterion.Id} | Category: {criterion.Category} | Type: {criterion.Type} | Name: {criterion.Name}"));
    }

    private static string BuildExistingSetupsDigest(IReadOnlyCollection<SetupSummaryDto> setups)
    {
        if (setups.Count == 0)
        {
            return "No existing setups found for this user.";
        }

        return string.Join("\n", setups.Select(setup =>
            $"- SetupId: {setup.Id} | Name: {setup.Name} | Description: {setup.Description ?? "None"} | Status: {setup.Status}"));
    }

    private static PreTradeChecklistInterpretationResultDto SanitizeChecklistInterpretationResult(
        PreTradeChecklistInterpretationResultDto response,
        ChecklistModelContextDto checklistModel)
    {
        Dictionary<int, ChecklistCriterionContextDto> criteriaById = checklistModel.Criteria.ToDictionary(criteria => criteria.Id);

        List<int> suggestedChecklistIds = [.. response.SuggestedChecklistIds
            .Where(criteriaById.ContainsKey)
            .Distinct()];

        Dictionary<int, PreTradeChecklistInterpretationMatchDto> matchesByChecklistId = [];

        foreach (PreTradeChecklistInterpretationMatchDto match in response.Matches)
        {
            if (!criteriaById.TryGetValue(match.ChecklistId, out ChecklistCriterionContextDto? criterion))
            {
                continue;
            }

            matchesByChecklistId[match.ChecklistId] = new PreTradeChecklistInterpretationMatchDto
            {
                ChecklistId = criterion.Id,
                ChecklistName = criterion.Name,
                Category = criterion.Category,
                Rationale = string.IsNullOrWhiteSpace(match.Rationale) ? "Matched from the trader's notes." : match.Rationale.Trim(),
                Confidence = Math.Clamp(match.Confidence, 0m, 1m)
            };
        }

        foreach (int checklistId in suggestedChecklistIds)
        {
            if (matchesByChecklistId.ContainsKey(checklistId))
            {
                continue;
            }

            ChecklistCriterionContextDto criterion = criteriaById[checklistId];
            matchesByChecklistId[checklistId] = new PreTradeChecklistInterpretationMatchDto
            {
                ChecklistId = criterion.Id,
                ChecklistName = criterion.Name,
                Category = criterion.Category,
                Rationale = "Suggested from the trader's notes.",
                Confidence = Math.Clamp(response.Confidence, 0m, 1m)
            };
        }

        return new PreTradeChecklistInterpretationResultDto
        {
            ChecklistModelId = checklistModel.Id,
            Summary = string.IsNullOrWhiteSpace(response.Summary) ? $"Mapped notes against {checklistModel.Name}." : response.Summary.Trim(),
            Confidence = Math.Clamp(response.Confidence, 0m, 1m),
            SuggestedChecklistIds = suggestedChecklistIds,
            Matches = [.. matchesByChecklistId.Values.OrderByDescending(match => match.Confidence).ThenBy(match => match.ChecklistName)],
            UnmatchedInputs = SanitizeStringList(response.UnmatchedInputs)
        };
    }

    private static TradingSetupGenerationResultDto SanitizeTradingSetupGenerationResult(
        TradingSetupGenerationResultDto response,
        int maxNodes)
    {
        List<TradingSetupGenerationNodeDto> nodes = SanitizeTradingSetupNodes(response.Nodes, maxNodes);

        if (nodes.Count == 0)
        {
            throw new InvalidOperationException("AI setup generation returned no valid nodes.");
        }

        HashSet<string> nodeIds = [.. nodes.Select(node => node.Id)];
        List<TradingSetupGenerationEdgeDto> edges = SanitizeTradingSetupEdges(response.Edges, nodeIds);

        if (nodes.Count > 1 && edges.Count == 0)
        {
            edges = CreateFallbackTradingSetupEdges(nodes);
        }

        return new TradingSetupGenerationResultDto
        {
            Summary = string.IsNullOrWhiteSpace(response.Summary) ? "Generated setup preview." : response.Summary.Trim(),
            Name = string.IsNullOrWhiteSpace(response.Name) ? "AI Setup Draft" : response.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(response.Description) ? null : response.Description.Trim(),
            Nodes = nodes,
            Edges = edges,
            Assumptions = SanitizeStringList(response.Assumptions),
            Warnings = SanitizeStringList(response.Warnings),
            Confidence = Math.Clamp(response.Confidence, 0m, 1m)
        };
    }

    private static List<TradingSetupGenerationNodeDto> SanitizeTradingSetupNodes(
        IReadOnlyList<TradingSetupGenerationNodeDto> nodes,
        int maxNodes)
    {
        List<TradingSetupGenerationNodeDto> sanitized = [];
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < nodes.Count && sanitized.Count < maxNodes; index++)
        {
            TradingSetupGenerationNodeDto node = nodes[index];
            string nodeId = string.IsNullOrWhiteSpace(node.Id) ? $"ai-node-{index + 1}" : node.Id.Trim();

            if (!seenIds.Add(nodeId))
            {
                continue;
            }

            (double x, double y) = GetSetupNodePosition(index, node.X, node.Y);

            sanitized.Add(new TradingSetupGenerationNodeDto
            {
                Id = nodeId,
                Kind = NormalizeSetupNodeKind(node.Kind),
                Title = string.IsNullOrWhiteSpace(node.Title) ? $"Step {sanitized.Count + 1}" : node.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(node.Notes) ? null : node.Notes.Trim(),
                X = x,
                Y = y,
            });
        }

        return sanitized;
    }

    private static List<TradingSetupGenerationEdgeDto> CreateFallbackTradingSetupEdges(IReadOnlyList<TradingSetupGenerationNodeDto> nodes)
    {
        List<TradingSetupGenerationEdgeDto> edges = [];

        for (int index = 0; index < nodes.Count - 1; index++)
        {
            edges.Add(new TradingSetupGenerationEdgeDto
            {
                Id = $"ai-edge-fallback-{index + 1}",
                Source = nodes[index].Id,
                Target = nodes[index + 1].Id,
                Label = null,
            });
        }

        return edges;
    }

    private static List<TradingSetupGenerationEdgeDto> SanitizeTradingSetupEdges(
        IReadOnlyList<TradingSetupGenerationEdgeDto> edges,
        IReadOnlySet<string> nodeIds)
    {
        List<TradingSetupGenerationEdgeDto> sanitized = [];
        HashSet<string> seenConnections = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < edges.Count; index++)
        {
            TradingSetupGenerationEdgeDto edge = edges[index];
            string source = edge.Source?.Trim() ?? string.Empty;
            string target = edge.Target?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (!nodeIds.Contains(source) || !nodeIds.Contains(target) || string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string connectionKey = $"{source}->{target}";
            if (!seenConnections.Add(connectionKey))
            {
                continue;
            }

            sanitized.Add(new TradingSetupGenerationEdgeDto
            {
                Id = string.IsNullOrWhiteSpace(edge.Id) ? $"ai-edge-{index + 1}" : edge.Id.Trim(),
                Source = source,
                Target = target,
                Label = string.IsNullOrWhiteSpace(edge.Label) ? null : edge.Label.Trim(),
            });
        }

        return sanitized;
    }

    private static string NormalizeSetupNodeKind(string? kind)
    {
        string normalizedKind = string.IsNullOrWhiteSpace(kind) ? "step" : kind.Trim().ToLowerInvariant();
        return AllowedSetupNodeKinds.Contains(normalizedKind) ? normalizedKind : "step";
    }

    private static (double X, double Y) GetSetupNodePosition(int index, double x, double y)
    {
        if (double.IsFinite(x) && double.IsFinite(y))
        {
            return (x, y);
        }

        const double startX = 140;
        const double startY = 80;
        const double gapX = 280;
        const double gapY = 170;
        const int columns = 3;

        int column = index % columns;
        int row = index / columns;
        return (startX + (column * gapX), startY + (row * gapY));
    }

    private async Task<List<byte[]>> LoadImageSourcesAsync(
        IReadOnlyList<string> imageSources,
        CancellationToken cancellationToken)
    {
        List<byte[]> images = [];

        foreach (string imageSource in imageSources.Where(source => !string.IsNullOrWhiteSpace(source)).Take(MaxChartAnalysisImages))
        {
            byte[]? imageBytes = TryDecodeImageDataUrl(imageSource);

            if (imageBytes is null)
            {
                imageBytes = await imageHelper.GetImagePartFromUrl(imageSource, cancellationToken);
            }

            if (imageBytes is { Length: > 0 })
            {
                images.Add(imageBytes);
            }
        }

        return images;
    }

    private static byte[]? TryDecodeImageDataUrl(string imageSource)
    {
        if (!SupportedInlineImagePrefixes.Any(prefix => imageSource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        int commaIndex = imageSource.IndexOf(',');

        if (commaIndex < 0 || commaIndex >= imageSource.Length - 1)
        {
            return null;
        }

        try
        {
            string base64Content = imageSource[(commaIndex + 1)..];
            int estimatedSize = (base64Content.Length * 3) / 4;

            if (estimatedSize <= 0 || estimatedSize > MaxInlineImageBytes)
            {
                return null;
            }

            byte[] imageBytes = Convert.FromBase64String(base64Content);

            return imageBytes.Length > MaxInlineImageBytes || !HasSupportedImageSignature(imageBytes)
                ? null
                : imageBytes;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool HasSupportedImageSignature(byte[] imageBytes)
    {
        if (imageBytes.Length < 4)
        {
            return false;
        }

        bool isPng = imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47;
        bool isJpeg = imageBytes.Length >= 3 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF;
        bool isWebp = imageBytes.Length >= 12
            && imageBytes[0] == 0x52
            && imageBytes[1] == 0x49
            && imageBytes[2] == 0x46
            && imageBytes[3] == 0x46
            && imageBytes[8] == 0x57
            && imageBytes[9] == 0x45
            && imageBytes[10] == 0x42
            && imageBytes[11] == 0x50;

        return isPng || isJpeg || isWebp;
    }

    private static List<SuggestedLessonDto> SanitizeSuggestedLessons(
        IReadOnlyList<SuggestedLessonDto>? suggestions,
        LessonSuggestionContextDto context)
    {
        if (suggestions is null || suggestions.Count == 0)
        {
            return [];
        }

        HashSet<string> existingTitles = new(
            context.ExistingLessons
                .Select(lesson => NormalizeLessonTitle(lesson.Title))
                .Where(title => !string.IsNullOrWhiteSpace(title)),
            StringComparer.OrdinalIgnoreCase);

        HashSet<int> allowedTradeIds = [.. context.Trades.Select(trade => trade.TradeId)];
        HashSet<string> returnedTitles = new(StringComparer.OrdinalIgnoreCase);
        List<SuggestedLessonDto> sanitized = [];

        foreach (SuggestedLessonDto suggestion in suggestions)
        {
            suggestion.Title = suggestion.Title.Trim();
            suggestion.Content = suggestion.Content.Trim();
            suggestion.KeyTakeaway = suggestion.KeyTakeaway?.Trim();
            suggestion.ActionItems = suggestion.ActionItems?.Trim();

            string normalizedTitle = NormalizeLessonTitle(suggestion.Title);

            if (string.IsNullOrWhiteSpace(normalizedTitle) || string.IsNullOrWhiteSpace(suggestion.Content))
            {
                continue;
            }

            if (existingTitles.Contains(normalizedTitle) || !returnedTitles.Add(normalizedTitle))
            {
                continue;
            }

            suggestion.Category = SanitizeLessonCategory(suggestion.Category);
            suggestion.Severity = SanitizeLessonSeverity(suggestion.Severity);
            suggestion.ImpactScore = Math.Clamp(suggestion.ImpactScore, 1, 10);
            suggestion.LinkedTradeIds = [.. suggestion.LinkedTradeIds
                .Where(allowedTradeIds.Contains)
                .Distinct()];

            sanitized.Add(suggestion);
        }

        return sanitized;
    }

    private static string NormalizeLessonTitle(string title)
    {
        return title.Trim().ToUpperInvariant();
    }

    private static int SanitizeLessonCategory(int category)
    {
        return category is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 99
            ? category
            : 99;
    }

    private static int SanitizeLessonSeverity(int severity)
    {
        return severity is 0 or 1 or 2
            ? severity
            : 1;
    }

    private static List<PlaybookOptimizationRecommendationDto> EnrichPlaybookRecommendations(
        IReadOnlyList<PlaybookOptimizationRecommendationDto>? recommendations,
        IReadOnlyList<PlaybookSetupSnapshot> playbookSnapshots)
    {
        if (recommendations is null || recommendations.Count == 0)
        {
            return [];
        }

        Dictionary<int, PlaybookSetupSnapshot> snapshotsById = playbookSnapshots.ToDictionary(snapshot => snapshot.SetupId);
        HashSet<int> seenSetupIds = [];
        List<PlaybookOptimizationRecommendationDto> enriched = [];

        foreach (PlaybookOptimizationRecommendationDto recommendation in recommendations)
        {
            if (!snapshotsById.TryGetValue(recommendation.SetupId, out PlaybookSetupSnapshot? snapshot) || !seenSetupIds.Add(recommendation.SetupId))
            {
                continue;
            }

            recommendation.Action = SanitizePlaybookAction(recommendation.Action);
            recommendation.Rationale = recommendation.Rationale.Trim();
            recommendation.Recommendation = recommendation.Recommendation.Trim();
            recommendation.Confidence = Math.Clamp(recommendation.Confidence, 0m, 1m);

            if (string.IsNullOrWhiteSpace(recommendation.Rationale) || string.IsNullOrWhiteSpace(recommendation.Recommendation))
            {
                continue;
            }

            recommendation.SetupName = snapshot.SetupName;
            recommendation.TotalTrades = snapshot.TotalTrades;
            recommendation.WinRate = snapshot.WinRate;
            recommendation.TotalPnl = snapshot.TotalPnl;
            recommendation.Expectancy = snapshot.Expectancy;
            recommendation.AvgRiskReward = snapshot.AvgRiskReward;
            recommendation.Grade = snapshot.Grade;

            enriched.Add(recommendation);
        }

        return enriched;
    }

    private static string SanitizePlaybookAction(string action)
    {
        string normalizedAction = action.Trim().ToLowerInvariant();

        return normalizedAction is "prioritize" or "refine" or "retire" or "observe"
            ? normalizedAction
            : "observe";
    }

    private static PlaybookSetupSnapshot BuildPlaybookSetupSnapshot(SetupSummaryDto setup, IReadOnlyList<TradeCacheDto> closedTrades)
    {
        List<TradeCacheDto> setupTrades = [.. closedTrades.Where(trade => trade.TradingSetupId == setup.Id)];
        List<TradeCacheDto> wins = [.. setupTrades.Where(trade => trade.Pnl > 0)];
        List<TradeCacheDto> losses = [.. setupTrades.Where(trade => trade.Pnl <= 0)];

        decimal totalPnl = setupTrades.Count > 0 ? setupTrades.Sum(trade => trade.Pnl!.Value) : 0;
        decimal winRate = setupTrades.Count > 0 ? (decimal)wins.Count / setupTrades.Count * 100 : 0;
        decimal avgWin = wins.Count > 0 ? wins.Average(trade => trade.Pnl!.Value) : 0;
        decimal avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(trade => trade.Pnl!.Value)) : 0;
        decimal grossProfit = wins.Sum(trade => trade.Pnl!.Value);
        decimal grossLoss = Math.Abs(losses.Sum(trade => trade.Pnl!.Value));
        decimal profitFactor = grossLoss > 0
            ? grossProfit / grossLoss
            : (grossProfit > 0 ? decimal.MaxValue : 0);
        decimal expectancy = (winRate / 100 * avgWin) - ((1 - winRate / 100) * avgLoss);
        decimal avgRiskReward = CalculateAverageRiskReward(setupTrades);
        string grade = CalculatePlaybookGrade(winRate, profitFactor, setupTrades.Count);

        return new PlaybookSetupSnapshot(
            setup.Id,
            setup.Name,
            setup.Description,
            setup.Status,
            setupTrades.Count,
            wins.Count,
            losses.Count,
            Math.Round(winRate, 1),
            Math.Round(totalPnl, 2),
            Math.Round(profitFactor, 2),
            Math.Round(expectancy, 2),
            Math.Round(avgRiskReward, 2),
            grade);
    }

    private static decimal CalculateAverageRiskReward(IEnumerable<TradeCacheDto> setupTrades)
    {
        double[] riskRewardValues = [.. setupTrades
            .Where(trade => trade.StopLoss > 0 && trade.TargetTier1 > 0 && trade.EntryPrice > 0)
            .Select(trade =>
            {
                decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
                decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);

                return risk > 0 ? (double)(reward / risk) : 0;
            })
            .Where(value => value > 0)];

        return riskRewardValues.Length > 0 ? (decimal)riskRewardValues.Average() : 0;
    }

    private static string CalculatePlaybookGrade(decimal winRate, decimal profitFactor, int totalTrades)
    {
        if (totalTrades < 5) return "N/A";
        if (winRate >= 65 && profitFactor >= 2) return "A";
        if (winRate >= 55 && profitFactor >= 1.5m) return "B";
        if (winRate >= 45 && profitFactor >= 1) return "C";
        if (winRate >= 35) return "D";
        return "F";
    }

    private static string BuildDateRangeSummary(DateTime? start, DateTime? end)
    {
        if (!start.HasValue && !end.HasValue)
        {
            return "All available setup-linked closed trades.";
        }

        return $"Closed setup-linked trades from {(start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "the beginning")} to {(end?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "now")}.";
    }

    private static string FormatPromptMetric(decimal value)
    {
        return value == decimal.MaxValue
            ? "Infinity"
            : FormatPromptNumber(value, 2);
    }

    private static List<string> SanitizeStringList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return [.. values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string BuildRiskAlertDigest(IReadOnlyList<RiskAdvisorAlertDto> alerts)
    {
        if (alerts.Count == 0)
        {
            return "No active risk alerts.";
        }

        return string.Join("\n", alerts.Select(alert =>
            $"- {alert.Severity.ToUpperInvariant()} | {alert.Title}: {alert.Message}"));
    }

    private static string BuildEconomicEventDigest(IReadOnlyList<EconomicImpactEventDto> events)
    {
        if (events.Count == 0)
        {
            return "No relevant upcoming high-impact events.";
        }

        return string.Join("\n", events.Select(e =>
            $"- {e.EventName} | {e.Currency} | {e.Impact} | Time: {e.EventDateUtc:yyyy-MM-dd HH:mm} UTC | MinutesUntil: {(e.MinutesUntilRelease?.ToString(CultureInfo.InvariantCulture) ?? "Released")} | Forecast: {(e.Forecast?.ToString(CultureInfo.InvariantCulture) ?? "N/A")} | Previous: {(e.Previous?.ToString(CultureInfo.InvariantCulture) ?? "N/A")}"));
    }

    private static string SanitizeRiskLevel(string riskLevel)
    {
        string normalized = riskLevel.Trim().ToLowerInvariant();

        return normalized is "low" or "moderate" or "high" or "critical"
            ? normalized
            : "moderate";
    }

    private sealed record PlaybookSetupSnapshot(
        int SetupId,
        string SetupName,
        string? Description,
        int Status,
        int TotalTrades,
        int Wins,
        int Losses,
        decimal WinRate,
        decimal TotalPnl,
        decimal ProfitFactor,
        decimal Expectancy,
        decimal AvgRiskReward,
        string Grade);

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
