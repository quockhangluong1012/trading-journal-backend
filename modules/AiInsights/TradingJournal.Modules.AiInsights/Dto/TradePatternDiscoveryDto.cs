using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record TradePatternDiscoveryRequestDto(
    DateTime? FromDate,
    DateTime? ToDate,
    int UserId = 0);

public sealed class TradePatternDiscoveryResultDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("patterns")]
    public List<DiscoveredTradePatternDto> Patterns { get; set; } = [];

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = [];

    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; set; }
}

public sealed class DiscoveredTradePatternDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}