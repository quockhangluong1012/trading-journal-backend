using TradingJournal.Modules.Scanner.Services.ICTAnalysis;

namespace TradingJournal.Modules.Scanner.Services;

public interface IScannerEngine
{
    /// <summary>
    /// Runs a scan for a specific watchlist — analyzes all assets in it using the user's global config.
    /// Returns the number of new alerts generated.
    /// </summary>
    Task<int> ScanForWatchlistAsync(int watchlistId, int userId, CancellationToken ct = default);
}
