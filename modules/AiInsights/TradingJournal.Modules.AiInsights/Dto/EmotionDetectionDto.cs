using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record EmotionDetectionRequestDto(
    string TextContent,
    int UserId = 0);

public class EmotionDetectionResultDto
{
    [JsonPropertyName("detectedEmotions")]
    public List<DetectedEmotionDto> DetectedEmotions { get; set; } = [];

    [JsonPropertyName("overallSentiment")]
    public string OverallSentiment { get; set; } = string.Empty;

    [JsonPropertyName("psychologySummary")]
    public string PsychologySummary { get; set; } = string.Empty;

    [JsonPropertyName("tradingReadiness")]
    public string TradingReadiness { get; set; } = string.Empty;

    [JsonPropertyName("tradingReadinessExplanation")]
    public string TradingReadinessExplanation { get; set; } = string.Empty;
}

public class DetectedEmotionDto
{
    [JsonPropertyName("emotionName")]
    public string EmotionName { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
