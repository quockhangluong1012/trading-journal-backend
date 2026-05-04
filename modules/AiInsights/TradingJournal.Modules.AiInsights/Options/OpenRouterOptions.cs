using System.ComponentModel.DataAnnotations;

namespace TradingJournal.Modules.AiInsights.Options;

public sealed class OpenRouterOptions
{
    public const string BindLocator = "OpenRouterAI";

    [Required(ErrorMessage = "OpenRouterAI:ApiKey is required.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "OpenRouterAI:Model is required.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "OpenRouterAI:BaseUrl is required.")]
    [Url(ErrorMessage = "OpenRouterAI:BaseUrl must be a valid URL.")]
    public string BaseUrl { get; set; } = string.Empty;
}
