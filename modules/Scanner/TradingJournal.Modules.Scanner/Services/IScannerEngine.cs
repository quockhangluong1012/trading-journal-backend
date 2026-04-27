using TradingJournal.Modules.Scanner.Services.ICTAnalysis;

namespace TradingJournal.Modules.Scanner.Services;

public interface IScannerEngine
{
    /// <summary>
    /// Runs a scan for a specific user — analyzes all assets in their active watchlists.
    /// Returns the number of new alerts generated.
    /// </summary>
    Task<int> ScanForUserAsync(int userId, CancellationToken ct = default);
}
