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
        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);
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
        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);
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
        string responseText = await SendDeepSeekRequest(finalPrompt, imageContents, cancellationToken);
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

}
