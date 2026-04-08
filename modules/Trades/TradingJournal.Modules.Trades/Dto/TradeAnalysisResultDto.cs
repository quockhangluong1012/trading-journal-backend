using System.Text.Json.Serialization;

namespace TradingJournal.Modules.Trades.Dto;

public class TradeAnalysisResultDto
{
    [JsonPropertyName("executiveSummary")]
    public string ExecutiveSummary { get; set; } = string.Empty;

    [JsonPropertyName("technicalInsights")]
    public string TechnicalInsights { get; set; } = string.Empty;

    [JsonPropertyName("psychologyAnalysis")]
    public string PsychologyAnalysis { get; set; } = string.Empty;

    [JsonPropertyName("criticalMistakes")]
    public CriticalMistakesDto CriticalMistakes { get; set; } = new();

    [JsonPropertyName("whatToImprove")]
    public List<string> WhatToImprove { get; set; } = [];
}

public class CriticalMistakesDto
{
    [JsonPropertyName("technical")]
    public List<string> Technical { get; set; } = [];

    [JsonPropertyName("psychological")]
    public List<string> Psychological { get; set; } = [];
}
