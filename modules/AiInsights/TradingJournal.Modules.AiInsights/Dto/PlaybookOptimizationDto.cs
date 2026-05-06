using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record PlaybookOptimizationRequestDto(
    DateTime? FromDate,
    DateTime? ToDate,
    int UserId);

public sealed class PlaybookOptimizationResultDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public List<PlaybookOptimizationRecommendationDto> Recommendations { get; set; } = [];

    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; set; }
}

public sealed class PlaybookOptimizationRecommendationDto
{
    [JsonPropertyName("setupId")]
    public int SetupId { get; set; }

    [JsonPropertyName("setupName")]
    public string SetupName { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = "observe";

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("totalTrades")]
    public int TotalTrades { get; set; }

    [JsonPropertyName("winRate")]
    public decimal WinRate { get; set; }

    [JsonPropertyName("totalPnl")]
    public decimal TotalPnl { get; set; }

    [JsonPropertyName("expectancy")]
    public decimal Expectancy { get; set; }

    [JsonPropertyName("avgRiskReward")]
    public decimal AvgRiskReward { get; set; }

    [JsonPropertyName("grade")]
    public string Grade { get; set; } = "N/A";
}