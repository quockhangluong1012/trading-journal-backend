using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record AiEconomicImpactPredictorRequestDto(
    string Symbol,
    int ProximityMinutes,
    int UserId = 0);

public sealed class AiEconomicImpactPredictorResultDto
{
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("tradeStance")]
    public string TradeStance { get; set; } = string.Empty;

    [JsonPropertyName("keyDrivers")]
    public List<string> KeyDrivers { get; set; } = [];

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = [];

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }
}