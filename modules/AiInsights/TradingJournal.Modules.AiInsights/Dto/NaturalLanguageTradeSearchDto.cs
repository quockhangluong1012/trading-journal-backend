using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record NaturalLanguageTradeSearchRequestDto(
    string Query,
    int UserId = 0);

public sealed record NaturalLanguageTradeSearchResultDto(
    [property: JsonPropertyName("asset")] string? Asset,
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("fromDate")] DateTime? FromDate,
    [property: JsonPropertyName("toDate")] DateTime? ToDate,
    [property: JsonPropertyName("interpretation")] string Interpretation);