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

internal sealed partial class OpenRouterAiService
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

}
