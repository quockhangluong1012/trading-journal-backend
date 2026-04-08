using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TradingJournal.Modules.Trades.Dto;
using TradingJournal.Modules.Trades.Extensions;
using TradingJournal.Modules.Trades.Options;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.Services;

internal sealed class OpenRouterAIService(
    IPromptService promptService,
    ITradeDbContext context,
    IEmotionTagProvider emotionTagProvider,
    IPsychologyProvider psychologyProvider,
    ITradeProvider tradeProvider,
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

        (TradeSumamryDto tradeSummary, List<string> psychologyNotes) = await BuildTradeSummaryDto(tradeHistory, cancellationToken);

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

    private async Task<(TradeSumamryDto Summary, List<string> PsychologyNotes)> BuildTradeSummaryDto(
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

        TradeSumamryDto summary = new(
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

    private static string BuildPrompt(string template, TradeSumamryDto summary, List<string> psychologyNotes)
    {
        Dictionary<string, string> replacements = new()
        {
            { "{{Asset}}", summary.Asset },
            { "{{Position}}", summary.Position },
            { "{{EntryPrice}}", summary.EntryPrice.ToString() },
            { "{{TargetTier1}}", summary.TargetTier1.ToString() },
            { "{{TargetTier2}}", summary.TargetTier2?.ToString() ?? string.Empty },
            { "{{TargetTier3}}", summary.TargetTier3?.ToString() ?? string.Empty },
            { "{{StopLoss}}", summary.StopLoss.ToString() },
            { "{{Notes}}", summary.Notes },
            { "{{ExitPrice}}", summary.ExitPrice?.ToString() ?? string.Empty },
            { "{{Pnl}}", summary.Pnl?.ToString() ?? string.Empty },
            { "{{ConfidenceLevel}}", summary.ConfidenceLevel },
            { "{{TradingZone}}", summary.TradingZone },
            { "{{Date}}", summary.OpenDate.ToShortDateString() },
            { "{{ClosedDate}}", summary.ClosedDate.ToShortDateString() },
            { "{{TradeTechnicalAnalysisTags}}", string.Join(", ", summary.TradeTechnicalAnalysisTags ?? []) },
            { "{{TradeHistoryChecklists}}", string.Join(", ", summary.TradeHistoryChecklists ?? []) },
            { "{{EmotionTags}}", string.Join(", ", summary.EmotionTags ?? []) },
            { "{{PsychologyNotes}}", string.Join(", ", psychologyNotes ?? []) }
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

        httpClient.BaseAddress = new Uri(options.Value.BaseUrl);

        using HttpRequestMessage request = new(HttpMethod.Post, $"api/v1/chat/completions");
        
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

        var (metrics, tradesList) = await BuildReviewMetricsAndTrades(request, cancellationToken);

        // Gather psychology notes for the period
        List<string> psychologyNotes = await psychologyProvider.GetPsychologyByDate(request.PeriodStart, cancellationToken);

        // Build prompt
        Dictionary<string, string> replacements = new()
        {
            { "{{PeriodType}}", request.PeriodType.ToString() },
            { "{{PeriodStart}}", request.PeriodStart.ToShortDateString() },
            { "{{PeriodEnd}}", request.PeriodEnd.ToShortDateString() },
            { "{{TotalPnl}}", metrics.TotalPnl.ToString("F2") },
            { "{{WinRate}}", metrics.WinRate.ToString("F1") },
            { "{{TotalTrades}}", metrics.TotalTrades.ToString() },
            { "{{Wins}}", metrics.Wins.ToString() },
            { "{{Losses}}", metrics.Losses.ToString() },
            { "{{TradesList}}", tradesList },
            { "{{PsychologyNotes}}", string.Join(", ", psychologyNotes ?? []) }
        };

        string finalPrompt = ReplacePlaceholders(promptTemplate, replacements);

        // Send to AI (text only, no images for review)
        string responseText = await SendOpenRouterRequest(finalPrompt, [], cancellationToken);

        return ParseReviewAiResponse(responseText);
    }

    private async Task<((int TotalTrades, int Wins, int Losses, double TotalPnl, double WinRate) Metrics, string TradesList)> BuildReviewMetricsAndTrades(
        ReviewSummaryRequestDto request, CancellationToken cancellationToken)
    {
        List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(cancellationToken);
        List<TradeCacheDto> periodTrades = [.. allTrades
            .Where(t => t.CreatedBy == request.UserId)
            .Where(t => t.Status == TradeStatus.Closed && t.Pnl.HasValue)
            .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value >= request.PeriodStart && t.ClosedDate.Value <= request.PeriodEnd)];

        int wins = periodTrades.Count(t => t.Pnl > 0);
        int losses = periodTrades.Count(t => t.Pnl <= 0);
        double totalPnl = periodTrades.Sum(t => (double)t.Pnl!.Value);
        double winRate = periodTrades.Count > 0 ? (double)wins / periodTrades.Count * 100 : 0;

        string tradesList = periodTrades.Count > 0
            ? string.Join("\n", periodTrades.Select(t =>
                $"- {t.Asset} | {t.Position} | Entry: {t.EntryPrice} | PnL: {t.Pnl} | Closed: {t.ClosedDate?.ToShortDateString()}"))
            : "Không có lệnh nào được đóng trong kỳ này.";

        return ((periodTrades.Count, wins, losses, totalPnl, winRate), tradesList);
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
}

