using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record SuggestLessonsRequestDto(
    DateTime? FromDate,
    DateTime? ToDate,
    int UserId);

public sealed class SuggestedLessonsResultDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("suggestions")]
    public List<SuggestedLessonDto> Suggestions { get; set; } = [];

    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; set; }
}

public sealed class SuggestedLessonDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public int Category { get; set; }

    [JsonPropertyName("severity")]
    public int Severity { get; set; }

    [JsonPropertyName("keyTakeaway")]
    public string? KeyTakeaway { get; set; }

    [JsonPropertyName("actionItems")]
    public string? ActionItems { get; set; }

    [JsonPropertyName("impactScore")]
    public int ImpactScore { get; set; }

    [JsonPropertyName("linkedTradeIds")]
    public List<int> LinkedTradeIds { get; set; } = [];
}