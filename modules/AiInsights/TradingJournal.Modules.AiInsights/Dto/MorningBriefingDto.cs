using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record MorningBriefingRequestDto(int UserId = 0);

public class MorningBriefingResultDto
{
    [JsonPropertyName("greeting")]
    public string Greeting { get; set; } = string.Empty;

    [JsonPropertyName("briefing")]
    public string Briefing { get; set; } = string.Empty;

    [JsonPropertyName("focusAreas")]
    public List<string> FocusAreas { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("actionItem")]
    public string ActionItem { get; set; } = string.Empty;

    [JsonPropertyName("overallMood")]
    public string OverallMood { get; set; } = string.Empty;
}
