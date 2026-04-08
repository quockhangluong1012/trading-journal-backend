namespace TradingJournal.Modules.Trades.Options;

public sealed class OpenRouterOptions
{
    public const string BindLocator = "OpenRouterAI";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;
}
