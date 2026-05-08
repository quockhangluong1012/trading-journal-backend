using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record TradingSetupGenerationRequestDto(
    string Prompt,
    int MaxNodes,
    bool DedupeAgainstExisting,
    int UserId = 0);

public sealed class TradingSetupGenerationResultDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("nodes")]
    public List<TradingSetupGenerationNodeDto> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<TradingSetupGenerationEdgeDto> Edges { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }
}

public sealed class TradingSetupGenerationNodeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class TradingSetupGenerationEdgeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}