using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record AiTiltInterventionRequestDto(
    int UserId,
    int TiltScore,
    string TiltLevel,
    int ConsecutiveLosses,
    int TradesLastHour,
    int RuleBreaksToday,
    decimal TodayPnl,
    DateTime? CooldownUntil);

public sealed class AiTiltInterventionResultDto
{
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [JsonPropertyName("tiltType")]
    public string TiltType { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = [];

    [JsonPropertyName("shouldNotify")]
    public bool ShouldNotify { get; set; }
}