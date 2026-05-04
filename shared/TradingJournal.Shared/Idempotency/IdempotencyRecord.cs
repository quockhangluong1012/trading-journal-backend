namespace TradingJournal.Shared.Idempotency;

/// <summary>
/// Stores a cached API response keyed by client-provided idempotency key.
/// Used to prevent duplicate mutations from network retries.
/// </summary>
public sealed class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
