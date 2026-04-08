namespace TradingJournal.Modules.Trades.Extensions;

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
            // Xử lý lỗi liên quan đến mạng (VD: 404 Not Found, 403 Forbidden)
            Console.WriteLine($"[HTTP Error] Lỗi tải ảnh từ URL: {imageUrl}. Chi tiết: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            // Xử lý các lỗi khác (VD: Hủy token)
            Console.WriteLine($"[Error] Không thể xử lý ảnh từ URL: {imageUrl}. Chi tiết: {ex.Message}");
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
