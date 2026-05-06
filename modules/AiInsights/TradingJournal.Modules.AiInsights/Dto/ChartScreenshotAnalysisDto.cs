using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record ChartScreenshotAnalysisRequestDto(
    string Asset,
    string Position,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TargetTier1,
    string? TradingZone,
    string? Notes,
    List<string> Screenshots,
    int UserId);

public sealed class ChartScreenshotAnalysisResultDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("marketStructure")]
    public string MarketStructure { get; set; } = string.Empty;

    [JsonPropertyName("amdPhase")]
    public string AmdPhase { get; set; } = string.Empty;

    [JsonPropertyName("premiumDiscount")]
    public string PremiumDiscount { get; set; } = string.Empty;

    [JsonPropertyName("confidenceScore")]
    public decimal ConfidenceScore { get; set; }

    [JsonPropertyName("keyLevels")]
    public List<string> KeyLevels { get; set; } = [];

    [JsonPropertyName("detectedConfluences")]
    public List<string> DetectedConfluences { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("suggestedActions")]
    public List<string> SuggestedActions { get; set; } = [];
}