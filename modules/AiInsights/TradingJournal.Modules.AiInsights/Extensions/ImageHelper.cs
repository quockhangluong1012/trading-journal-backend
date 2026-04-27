namespace TradingJournal.Modules.AiInsights.Extensions;

internal class ImageHelper(HttpClient httpClient) : IImageHelper
{
    public async Task<byte[]?> GetImagePartFromUrl(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return default;
        }

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(imageUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return imageBytes;
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"[HTTP Error] Failed to download image from URL: {imageUrl}. Details: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to process image from URL: {imageUrl}. Details: {ex.Message}");
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
}
