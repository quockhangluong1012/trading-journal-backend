using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Implementation of IScreenshotService that delegates file operations to IFileStorageService.
/// </summary>
internal sealed class ScreenshotService(IFileStorageService fileStorageService) : IScreenshotService
{
    private const string ScreenshotFolder = "screenshots";

    public async Task<string> SaveScreenshotAsync(string base64String, CancellationToken cancellationToken = default)
    {
        string rawBase64 = StripDataUriPrefix(base64String);
        byte[] imageBytes = Convert.FromBase64String(rawBase64);

        string fileName = $"{Guid.NewGuid()}.png";
        return await fileStorageService.SaveFileAsync(imageBytes, fileName, ScreenshotFolder, cancellationToken);
    }

    public async Task DeleteScreenshotAsync(string url, CancellationToken cancellationToken = default)
    {
        await fileStorageService.DeleteFileAsync(url, ScreenshotFolder, cancellationToken);
    }

    public bool IsBase64Image(string value)
    {
        return value.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
            || (!value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("/", StringComparison.Ordinal));
    }

    private static string StripDataUriPrefix(string base64String)
    {
        int commaIndex = base64String.IndexOf(',');
        return commaIndex >= 0 ? base64String[(commaIndex + 1)..] : base64String;
    }
}
