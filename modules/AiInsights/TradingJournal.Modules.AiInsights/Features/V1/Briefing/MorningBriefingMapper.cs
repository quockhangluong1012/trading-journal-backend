using System.Text.Json;
using TradingJournal.Modules.AiInsights.Dto;

namespace TradingJournal.Modules.AiInsights.Features.V1.Briefing;

internal static class MorningBriefingMapper
{
    public static MorningBriefingResultDto ToDto(MorningBriefing briefing)
    {
        return new MorningBriefingResultDto
        {
            Greeting = briefing.Greeting,
            Briefing = briefing.Briefing,
            ActionItem = briefing.ActionItem,
            OverallMood = briefing.OverallMood,
            FocusAreas = DeserializeList(briefing.FocusAreasJson),
            Warnings = DeserializeList(briefing.WarningsJson),
        };
    }

    public static MorningBriefing CreateEntity(MorningBriefingResultDto result, DateOnly briefingDateUtc)
    {
        return new MorningBriefing
        {
            BriefingDateUtc = briefingDateUtc,
            Greeting = result.Greeting.Trim(),
            Briefing = result.Briefing.Trim(),
            ActionItem = result.ActionItem.Trim(),
            OverallMood = result.OverallMood.Trim(),
            FocusAreasJson = SerializeList(result.FocusAreas),
            WarningsJson = SerializeList(result.Warnings),
        };
    }

    public static void Apply(MorningBriefing entity, MorningBriefingResultDto result, DateOnly briefingDateUtc)
    {
        entity.BriefingDateUtc = briefingDateUtc;
        entity.Greeting = result.Greeting.Trim();
        entity.Briefing = result.Briefing.Trim();
        entity.ActionItem = result.ActionItem.Trim();
        entity.OverallMood = result.OverallMood.Trim();
        entity.FocusAreasJson = SerializeList(result.FocusAreas);
        entity.WarningsJson = SerializeList(result.Warnings);
    }

    private static string SerializeList(IEnumerable<string>? values)
    {
        List<string> sanitizedValues = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return JsonSerializer.Serialize(sanitizedValues);
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}