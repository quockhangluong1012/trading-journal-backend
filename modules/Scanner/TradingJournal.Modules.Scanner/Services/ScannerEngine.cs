using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Scanner.Events;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;
using TradingJournal.Modules.Scanner.Services.LiveData;

namespace TradingJournal.Modules.Scanner.Services;

internal sealed class ScannerEngine(
    IScannerDbContext scannerDb,
    ILiveMarketDataProvider liveDataProvider,
    MultiTimeframeAnalyzer analyzer,
    IEnumerable<IMultiAssetDetector> multiAssetDetectors,
    IEventBus eventBus,
    ILogger<ScannerEngine> logger) : IScannerEngine
{
    /// <summary>
    /// Dedup window: don't re-alert for the same pattern+symbol+timeframe within this period.
    /// </summary>
    private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(4);

    /// <summary>
    /// Number of candles to fetch per timeframe for analysis.
    /// </summary>
    private const int CandlesPerTimeframe = 100;

    public async Task<int> ScanForWatchlistAsync(int watchlistId, int userId, CancellationToken ct = default)
    {
        // 1. Load user config with navigation properties
        ScannerConfig? config = await scannerDb.ScannerConfigs
            .Include(c => c.EnabledPatterns)
            .Include(c => c.EnabledTimeframes)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (config is null) return 0;

        List<IctPatternType> globalEnabledPatterns = config.EnabledPatterns
            .Select(p => p.PatternType)
            .ToList();

        List<ScannerTimeframe> enabledTimeframes = config.EnabledTimeframes
            .Select(t => t.Timeframe)
            .ToList();

        if (globalEnabledPatterns.Count == 0 || enabledTimeframes.Count == 0) return 0;

        // 2. Load assets for this specific watchlist (with per-asset detector overrides)
        var assets = await scannerDb.WatchlistAssets
            .Where(a => a.WatchlistId == watchlistId && !a.IsDisabled)
            .Include(a => a.EnabledDetectors)
            .ToListAsync(ct);

        var symbols = assets.Select(a => a.Symbol).Distinct().ToList();
        if (symbols.Count == 0) return 0;

        // 3. Build per-asset enabled patterns map (with fallback to global config)
        var perAssetPatterns = new Dictionary<string, List<IctPatternType>>();
        foreach (string symbol in symbols)
        {
            var assetDetectors = assets
                .Where(a => a.Symbol == symbol)
                .SelectMany(a => a.EnabledDetectors)
                .Where(d => d.IsEnabled && !d.IsDisabled)
                .Select(d => d.PatternType)
                .Distinct()
                .ToList();

            // Fallback: if no per-asset config exists, use global config
            perAssetPatterns[symbol] = assetDetectors.Count > 0
                ? assetDetectors
                : globalEnabledPatterns;
        }

        // 4. Prefetch LIVE candle data for all symbols+timeframes
        var allCandleData = new Dictionary<(string Symbol, ScannerTimeframe Tf), List<CandleData>>();

        foreach (string symbol in symbols)
        {
            foreach (ScannerTimeframe tf in enabledTimeframes)
            {
                List<CandleData> candles = await liveDataProvider.GetRecentCandlesAsync(
                    symbol, tf, CandlesPerTimeframe, ct);

                if (candles.Count > 0)
                {
                    allCandleData[(symbol, tf)] = candles;
                }
            }
        }

        int totalAlerts = 0;

        // 5. Run single-asset detectors
        foreach (string symbol in symbols)
        {
            try
            {
                var candlesByTimeframe = enabledTimeframes
                    .Where(tf => allCandleData.ContainsKey((symbol, tf)))
                    .ToDictionary(tf => tf, tf => allCandleData[(symbol, tf)]);

                var effectivePatterns = perAssetPatterns[symbol];

                int alertsForSymbol = await ProcessDetectionsAsync(
                    userId, symbol, watchlistId,
                    analyzer.Analyze(symbol, candlesByTimeframe, effectivePatterns),
                    config.MinConfluenceScore, ct);

                totalAlerts += alertsForSymbol;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error scanning symbol {Symbol} in watchlist {WatchlistId} for user {UserId}",
                    symbol, watchlistId, userId);
            }
        }

        // 6. Run multi-asset detectors (SMT Divergence)
        foreach (string symbol in symbols)
        {
            var effectivePatterns = perAssetPatterns[symbol];
            if (!effectivePatterns.Contains(IctPatternType.SMTDivergence)) continue;

            string? correlatedSymbol = SmtDivergenceDetector.GetCorrelatedSymbol(symbol);
            if (correlatedSymbol is null) continue;

            try
            {
                foreach (ScannerTimeframe tf in enabledTimeframes)
                {
                    if (!allCandleData.TryGetValue((symbol, tf), out var primaryCandles)) continue;

                    // Try to get correlated candles — may need to fetch if not in watchlist
                    if (!allCandleData.TryGetValue((correlatedSymbol, tf), out var correlatedCandles))
                    {
                        correlatedCandles = await liveDataProvider.GetRecentCandlesAsync(
                            correlatedSymbol, tf, CandlesPerTimeframe, ct);
                    }

                    if (correlatedCandles.Count == 0) continue;

                    foreach (var detector in multiAssetDetectors)
                    {
                        var smtPatterns = detector.Detect(
                            symbol, primaryCandles, correlatedSymbol, correlatedCandles, tf);

                        var smtResults = smtPatterns.Select(p => (p, ConfluenceScore: 1)).ToList();

                        int smtAlerts = await ProcessDetectionsAsync(
                            userId, symbol, watchlistId, smtResults,
                            config.MinConfluenceScore, ct);

                        totalAlerts += smtAlerts;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running SMT Divergence for {Symbol} vs {Correlated} in watchlist {WatchlistId}",
                    symbol, correlatedSymbol, watchlistId);
            }
        }

        return totalAlerts;
    }

    /// <summary>
    /// Processes detections: deduplicates, persists alerts, and publishes events.
    /// </summary>
    private async Task<int> ProcessDetectionsAsync(
        int userId,
        string symbol,
        int watchlistId,
        List<(DetectedPattern Pattern, int ConfluenceScore)> detections,
        int minConfluenceScore,
        CancellationToken ct)
    {
        var qualifiedDetections = detections
            .Where(d => d.ConfluenceScore >= minConfluenceScore)
            .ToList();

        int newAlerts = 0;

        foreach (var (pattern, confluenceScore) in qualifiedDetections)
        {
            // Deduplication check
            bool isDuplicate = await scannerDb.ScannerAlerts.AnyAsync(a =>
                a.UserId == userId &&
                a.Symbol == symbol &&
                a.PatternType == pattern.Type &&
                a.DetectionTimeframe == pattern.Timeframe &&
                a.DetectedAt > DateTime.UtcNow - DedupWindow &&
                !a.IsDisabled, ct);

            if (isDuplicate) continue;

            // Save alert
            var alert = new ScannerAlert
            {
                Id = default!,
                UserId = userId,
                Symbol = symbol,
                PatternType = pattern.Type,
                Timeframe = pattern.Timeframe,
                DetectionTimeframe = pattern.Timeframe,
                PriceAtDetection = pattern.PriceAtDetection,
                ZoneHighPrice = pattern.ZoneHigh,
                ZoneLowPrice = pattern.ZoneLow,
                Description = pattern.Description,
                ConfluenceScore = confluenceScore,
                DetectedAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };

            scannerDb.ScannerAlerts.Add(alert);
            await scannerDb.SaveChangesAsync(ct);

            // Publish integration event for the Notification module
            await eventBus.PublishAsync(new ScannerAlertEvent(
                EventId: Guid.NewGuid(),
                UserId: userId,
                Symbol: symbol,
                PatternType: pattern.Type.ToString(),
                Timeframe: pattern.Timeframe.ToString(),
                Price: pattern.PriceAtDetection,
                Description: pattern.Description,
                ConfluenceScore: confluenceScore), ct);

            newAlerts++;

            logger.LogInformation(
                "Scanner alert: {Pattern} on {Symbol} ({Timeframe}) in watchlist {WatchlistId}, confluence={Score}",
                pattern.Type, symbol, pattern.Timeframe, watchlistId, confluenceScore);
        }

        return newAlerts;
    }
}
