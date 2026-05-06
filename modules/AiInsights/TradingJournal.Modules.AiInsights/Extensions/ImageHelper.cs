using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.AiInsights.Extensions;

internal sealed class ImageHelper(HttpClient httpClient, ILogger<ImageHelper> logger) : IImageHelper
{
    private const int MaxImageSizeBytes = 5 * 1024 * 1024;

    public async Task<byte[]?> GetImagePartFromUrl(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return default;
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? imageUri) || imageUri.Scheme != Uri.UriSchemeHttps)
        {
            logger.LogWarning("Rejected non-HTTPS image URL for AI analysis.");
            return default;
        }

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(imageUri.Host, cancellationToken);

            if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
            {
                logger.LogWarning("Rejected remote image URL because it resolves to a blocked address. Host: {Host}", imageUri.Host);
                return default;
            }

            using HttpRequestMessage request = new(HttpMethod.Get, imageUri);
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            if (!response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                logger.LogWarning("Rejected non-image response for AI analysis. Content-Type: {ContentType}", response.Content.Headers.ContentType?.MediaType);
                return default;
            }

            if (response.Content.Headers.ContentLength is > MaxImageSizeBytes)
            {
                logger.LogWarning("Rejected oversized remote image for AI analysis. Content-Length: {ContentLength}", response.Content.Headers.ContentLength);
                return default;
            }

            await response.Content.LoadIntoBufferAsync(MaxImageSizeBytes, cancellationToken);

            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            if (imageBytes.Length == 0 || imageBytes.Length > MaxImageSizeBytes)
            {
                logger.LogWarning("Rejected image payload after download because size was outside accepted bounds. Size: {Size}", imageBytes.Length);
                return default;
            }

            return imageBytes;
        }
        catch (HttpRequestException httpEx)
        {
            logger.LogWarning(httpEx, "Failed to download image for AI analysis from host {Host}", imageUri.Host);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process image for AI analysis from host {Host}", imageUri.Host);
        }

        return default;
    }

    public async Task<List<byte[]>> GetImageBytesFromUrls(List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        List<byte[]> imageBytes = [];

        foreach (string imageUrl in imageUrls)
        {
            byte[]? bytes = await GetImagePartFromUrl(imageUrl, cancellationToken);

            if (bytes != null)
            {
                imageBytes.Add(bytes);
            }
        }

        return imageBytes;
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }
            else
            {
                return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
            }
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();

        return bytes[0] == 10
            || bytes[0] == 127
            || bytes[0] == 0
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
