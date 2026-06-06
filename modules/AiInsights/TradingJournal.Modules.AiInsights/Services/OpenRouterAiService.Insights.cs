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

}
