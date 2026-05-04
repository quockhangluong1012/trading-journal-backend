using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Implementation of IScreenshotService that delegates file operations to IFileStorageService.
/// Includes MIME type validation and magic byte verification to prevent malicious uploads.
/// </summary>
internal sealed class ScreenshotService(IFileStorageService fileStorageService) : IScreenshotService
{
    private const string ScreenshotFolder = "screenshots";
    private const int MaxScreenshotsPerTrade = 10;

    /// <summary>
    /// Allowed MIME types for screenshot uploads.
    /// </summary>
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/gif",
        "image/webp"
    };

    /// <summary>
    /// Magic byte signatures for known image formats.
    /// </summary>
    private static readonly (string MimeType, byte[] MagicBytes)[] ImageSignatures =
    [
        ("image/png", [0x89, 0x50, 0x4E, 0x47]),     // PNG: ‰PNG
        ("image/jpeg", [0xFF, 0xD8, 0xFF]),           // JPEG: ÿØÿ
        ("image/gif", [0x47, 0x49, 0x46]),            // GIF: GIF
        ("image/webp", [0x52, 0x49, 0x46, 0x46]),    // WebP: RIFF
    ];

    public async Task<string> SaveScreenshotAsync(string base64String, CancellationToken cancellationToken = default)
    {
        string mimeType = ExtractAndValidateMimeType(base64String);
        string rawBase64 = StripDataUriPrefix(base64String);
        byte[] imageBytes = Convert.FromBase64String(rawBase64);

        ValidateMagicBytes(imageBytes, mimeType);

        string extension = GetExtensionFromMimeType(mimeType);
        string fileName = $"{Guid.NewGuid()}{extension}";
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

    public void ValidateScreenshotCount(int count)
    {
        if (count > MaxScreenshotsPerTrade)
        {
            throw new InvalidOperationException(
                $"A trade can have at most {MaxScreenshotsPerTrade} screenshots. Received {count}.");
        }
    }

    /// <summary>
    /// Extracts MIME type from the data URI prefix and validates it against the allowed list.
    /// </summary>
    private static string ExtractAndValidateMimeType(string base64String)
    {
        // Default to image/png for raw base64 without a data URI prefix
        if (!base64String.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        int semicolonIndex = base64String.IndexOf(';');
        if (semicolonIndex < 0)
        {
            throw new InvalidOperationException("Invalid data URI format: missing semicolon separator.");
        }

        string mimeType = base64String[5..semicolonIndex]; // skip "data:"

        if (!AllowedMimeTypes.Contains(mimeType))
        {
            throw new InvalidOperationException(
                $"Unsupported image MIME type '{mimeType}'. Allowed types: {string.Join(", ", AllowedMimeTypes)}.");
        }

        return mimeType;
    }

    /// <summary>
    /// Verifies the decoded image bytes contain valid magic bytes for the claimed MIME type.
    /// </summary>
    private static void ValidateMagicBytes(byte[] imageBytes, string claimedMimeType)
    {
        if (imageBytes.Length < 4)
        {
            throw new InvalidOperationException("Image data is too small to be a valid image file.");
        }

        bool matchesAnySignature = false;

        foreach (var (_, magicBytes) in ImageSignatures)
        {
            if (imageBytes.Length >= magicBytes.Length &&
                imageBytes.AsSpan(0, magicBytes.Length).SequenceEqual(magicBytes))
            {
                matchesAnySignature = true;
                break;
            }
        }

        if (!matchesAnySignature)
        {
            throw new InvalidOperationException(
                $"Uploaded file does not match any known image format. Claimed MIME type: '{claimedMimeType}'.");
        }
    }

    private static string GetExtensionFromMimeType(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".png"
    };

    private static string StripDataUriPrefix(string base64String)
    {
        int commaIndex = base64String.IndexOf(',');
        return commaIndex >= 0 ? base64String[(commaIndex + 1)..] : base64String;
    }
}
