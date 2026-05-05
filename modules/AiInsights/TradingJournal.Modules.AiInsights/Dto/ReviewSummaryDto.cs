using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public record ReviewSummaryRequestDto(
    ReviewPeriodType PeriodType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int UserId);

public class ReviewSummaryResultDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("strengthsAnalysis")]
    public string StrengthsAnalysis { get; set; } = string.Empty;

    [JsonPropertyName("weaknessAnalysis")]
    public string WeaknessAnalysis { get; set; } = string.Empty;

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = [];

    [JsonPropertyName("technicalInsights")]
    public string TechnicalInsights { get; set; } = string.Empty;

    [JsonPropertyName("psychologyAnalysis")]
    public string PsychologyAnalysis { get; set; } = string.Empty;

    [JsonPropertyName("criticalMistakes")]
    public ReviewCriticalMistakesDto CriticalMistakes { get; set; } = new();

    [JsonPropertyName("whatToImprove")]
    public List<string> WhatToImprove { get; set; } = [];
}

public class ReviewCriticalMistakesDto
{
    [JsonPropertyName("technical")]
    public List<string> Technical { get; set; } = [];

    [JsonPropertyName("psychological")]
    public List<string> Psychological { get; set; } = [];
}
