namespace TradingJournal.Shared.Idempotency;

/// <summary>
/// Abstraction for idempotency key storage.
/// Supports saving and retrieving cached responses, plus TTL-based cleanup.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to retrieve a cached response for the given idempotency key.
    /// Returns null if the key does not exist or has expired.
    /// </summary>
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Saves a response for the given idempotency key.
    /// If the key already exists, the operation is idempotent and returns false.
    /// </summary>
    Task<bool> SaveAsync(IdempotencyRecord record, CancellationToken ct = default);

    /// <summary>
    /// Removes expired idempotency records older than the specified TTL.
    /// </summary>
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
