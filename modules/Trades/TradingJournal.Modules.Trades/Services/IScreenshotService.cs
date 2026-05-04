namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Handles screenshot file operations for trades — saving base64 images,
/// determining which screenshots to keep/remove on update, and cleaning up files.
/// Extracted from CreateTrade/UpdateTrade handlers to follow SRP.
/// </summary>
public interface IScreenshotService
{
    /// <summary>
    /// Saves a base64-encoded image and returns the URL.
    /// </summary>
    Task<string> SaveScreenshotAsync(string base64String, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a screenshot file by its URL.
    /// </summary>
    Task DeleteScreenshotAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a string is a base64-encoded image (vs an existing URL).
    /// </summary>
    bool IsBase64Image(string value);

    /// <summary>
    /// Validates the number of screenshots does not exceed the per-trade limit.
    /// Throws InvalidOperationException if exceeded.
    /// </summary>
    void ValidateScreenshotCount(int count);
}
