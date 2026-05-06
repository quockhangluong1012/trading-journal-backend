using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record AiRiskAdvisorRequestDto(int UserId = 0);

public sealed class AiRiskAdvisorResultDto
{
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("positionSizingAdvice")]
    public string PositionSizingAdvice { get; set; } = string.Empty;

    [JsonPropertyName("keyRisks")]
    public List<string> KeyRisks { get; set; } = [];

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = [];

    [JsonPropertyName("shouldReduceRisk")]
    public bool ShouldReduceRisk { get; set; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }
}