using System.Text.Json.Serialization;

namespace TradingJournal.Modules.AiInsights.Dto;

public sealed record PreTradeChecklistInterpretationRequestDto(
    int ChecklistModelId,
    string Input,
    int UserId = 0);

public sealed class PreTradeChecklistInterpretationResultDto
{
    [JsonPropertyName("checklistModelId")]
    public int ChecklistModelId { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("suggestedChecklistIds")]
    public List<int> SuggestedChecklistIds { get; set; } = [];

    [JsonPropertyName("matches")]
    public List<PreTradeChecklistInterpretationMatchDto> Matches { get; set; } = [];

    [JsonPropertyName("unmatchedInputs")]
    public List<string> UnmatchedInputs { get; set; } = [];
}

public sealed class PreTradeChecklistInterpretationMatchDto
{
    [JsonPropertyName("checklistId")]
    public int ChecklistId { get; set; }

    [JsonPropertyName("checklistName")]
    public string ChecklistName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }
}