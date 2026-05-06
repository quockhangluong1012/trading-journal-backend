using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record AiWeeklyDigestRequestDto(
    DateTime ReferenceDate,
    int UserId = 0);

public sealed class AiWeeklyDigestResultDto
{
    [JsonPropertyName("headline")]
    public string Headline { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("keyWins")]
    public List<string> KeyWins { get; set; } = [];

    [JsonPropertyName("keyRisks")]
    public List<string> KeyRisks { get; set; } = [];

    [JsonPropertyName("focusForNextWeek")]
    public string FocusForNextWeek { get; set; } = string.Empty;

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = [];
}