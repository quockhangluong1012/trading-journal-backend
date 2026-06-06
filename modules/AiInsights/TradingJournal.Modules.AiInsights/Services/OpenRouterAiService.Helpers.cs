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

    internal static string SanitizePromptInput(string input)
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

    [GeneratedRegex("```.*?```", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex PromptCodeFencePatternRegex();
    [GeneratedRegex(@"(?im)^\s*(system|assistant|developer|user)\s*:\s*", RegexOptions.Compiled, "en-US")]
    private static partial Regex PromptRolePrefixPatternRegex();
}
