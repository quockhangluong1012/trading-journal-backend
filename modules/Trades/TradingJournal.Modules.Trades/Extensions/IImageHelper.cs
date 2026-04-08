namespace TradingJournal.Modules.Trades.Extensions;

public interface IImageHelper
{
    public Task<byte[]>? GetImagePartFromUrl(string imageUrl, CancellationToken cancellationToken = default);

    public Task<List<byte[]>> GetImageBytesFromUrls(List<string> imageUrls, CancellationToken cancellationToken = default);
}
