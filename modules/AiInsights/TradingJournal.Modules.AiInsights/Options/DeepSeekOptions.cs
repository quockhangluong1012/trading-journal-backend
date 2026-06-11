using System.ComponentModel.DataAnnotations;

namespace TradingJournal.Modules.AiInsights.Options;

public sealed class DeepSeekOptions
{
    public const string BindLocator = "DeepSeek";

    [Required(ErrorMessage = "DeepSeek:ApiKey is required.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "DeepSeek:Model is required.")]
    public string Model { get; set; } = string.Empty;

    public string? AiCoachResearchModel { get; set; }

    public string? DeepResearchModel { get; set; }

    [Required(ErrorMessage = "DeepSeek:BaseUrl is required.")]
    [Url(ErrorMessage = "DeepSeek:BaseUrl must be a valid URL.")]
    public string BaseUrl { get; set; } = string.Empty;

    public bool Thinking { get; set; }

    public string ReasoningEffort { get; set; } = string.Empty;
}
