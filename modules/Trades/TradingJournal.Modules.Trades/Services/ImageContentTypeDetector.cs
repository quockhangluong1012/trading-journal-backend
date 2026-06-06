namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Detects an image's MIME type from its leading "magic bytes" rather than any client-supplied
/// header. Used to stop a caller spoofing <c>Content-Type</c> to smuggle a non-image (or a
/// different image type) past the upload pipeline.
/// </summary>
internal static class ImageContentTypeDetector
{
    private const int MinimumLength = 4;

    /// <summary>
    /// Returns the canonical MIME type for the supplied bytes, or <c>null</c> when the content
    /// does not match a supported image format (PNG, JPEG, GIF, WebP).
    /// </summary>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < MinimumLength)
        {
            return null;
        }

        // PNG: 89 50 4E 47 ("\x89PNG")
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // GIF: "GIF"
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
        {
            return "image/gif";
        }

        // WebP: "RIFF"...."WEBP" — RIFF alone is shared with WAV/AVI, so require the WEBP marker.
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }
}
