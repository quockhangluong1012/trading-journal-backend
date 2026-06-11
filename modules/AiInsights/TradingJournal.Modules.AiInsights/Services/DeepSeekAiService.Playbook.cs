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
        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);

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
        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);

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
        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);
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
        string responseText = await SendDeepSeekRequest(finalPrompt, [], cancellationToken);

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

}
