using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record PreTradeValidationRequestDto(
    string Asset,
    string Position,
    decimal EntryPrice,
    decimal StopLoss,
    decimal TargetTier1,
    decimal? TargetTier2,
    decimal? TargetTier3,
    int ConfidenceLevel,
    string? TradingZone,
    List<string>? TechnicalAnalysisTags,
    string? ChecklistStatus,
    List<string>? EmotionTags,
    string? Notes,
    int UserId = 0);

public class PreTradeValidationResultDto
{
    [JsonPropertyName("grade")]
    public string Grade { get; set; } = string.Empty;

    [JsonPropertyName("gradeExplanation")]
    public string GradeExplanation { get; set; } = string.Empty;

    [JsonPropertyName("ictAlignment")]
    public string IctAlignment { get; set; } = string.Empty;

    [JsonPropertyName("riskRewardAssessment")]
    public string RiskRewardAssessment { get; set; } = string.Empty;

    [JsonPropertyName("emotionalReadiness")]
    public string EmotionalReadiness { get; set; } = string.Empty;

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = [];

    [JsonPropertyName("shouldProceed")]
    public bool ShouldProceed { get; set; }
}
