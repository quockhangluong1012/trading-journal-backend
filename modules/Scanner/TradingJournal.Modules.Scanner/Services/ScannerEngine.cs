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
    private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(4);
    private const int CandlesPerTimeframe = 100;

    public async Task<int> ScanForWatchlistAsync(int watchlistId, int userId, CancellationToken ct = default)
    {
        ScannerConfig? config = await scannerDb.ScannerConfigs
            .Include(c => c.EnabledPatterns)
            .Include(c => c.EnabledTimeframes)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (config is null) return 0;

        List<IctPatternType> globalEnabledPatterns = config.EnabledPatterns.Select(p => p.PatternType).ToList();
        List<ScannerTimeframe> enabledTimeframes = config.EnabledTimeframes.Select(t => t.Timeframe).ToList();
        if (globalEnabledPatterns.Count == 0 || enabledTimeframes.Count == 0) return 0;

        var assets = await scannerDb.WatchlistAssets
            .Where(a => a.WatchlistId == watchlistId && !a.IsDisabled)
            .Include(a => a.EnabledDetectors)
            .ToListAsync(ct);

        var symbols = assets.Select(a => a.Symbol).Distinct().ToList();
        if (symbols.Count == 0) return 0;

        var perAssetPatterns = BuildPerAssetPatterns(assets, symbols, globalEnabledPatterns);
        var allCandleData = await FetchAllCandleDataAsync(symbols, enabledTimeframes, ct);

        // Prefetch existing alerts for dedup in one query instead of N queries in loop
        var dedupCutoff = DateTime.UtcNow - DedupWindow;
        var existingAlerts = await scannerDb.ScannerAlerts.AsNoTracking()
            .Where(a => a.UserId == userId && symbols.Contains(a.Symbol) && a.DetectedAt > dedupCutoff && !a.IsDisabled)
            .Select(a => new { a.Symbol, a.PatternType, a.DetectionTimeframe })
            .ToListAsync(ct);
        var existingKeys = new HashSet<(string, IctPatternType, ScannerTimeframe)>(
            existingAlerts.Select(a => (a.Symbol, a.PatternType, a.DetectionTimeframe)));

        int totalAlerts = 0;

        foreach (string symbol in symbols)
        {
            try
            {
                var candlesByTf = enabledTimeframes
                    .Where(tf => allCandleData.ContainsKey((symbol, tf)))
                    .ToDictionary(tf => tf, tf => allCandleData[(symbol, tf)]);

                totalAlerts += await ProcessDetectionsAsync(userId, symbol, watchlistId,
                    analyzer.Analyze(symbol, candlesByTf, perAssetPatterns[symbol]),
                    config.MinConfluenceScore, candlesByTf, existingKeys, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error scanning {Symbol} in watchlist {WatchlistId}", symbol, watchlistId);
            }
        }

        // Multi-asset detectors (SMT Divergence)
        foreach (string symbol in symbols)
        {
            if (!perAssetPatterns[symbol].Contains(IctPatternType.SMTDivergence)) continue;
            string? corr = SmtDivergenceDetector.GetCorrelatedSymbol(symbol);
            if (corr is null) continue;

            try
            {
                foreach (ScannerTimeframe tf in enabledTimeframes)
                {
                    if (!allCandleData.TryGetValue((symbol, tf), out var primary)) continue;
                    if (!allCandleData.TryGetValue((corr, tf), out var corrCandles))
                        corrCandles = await liveDataProvider.GetRecentCandlesAsync(corr, tf, CandlesPerTimeframe, ct);
                    if (corrCandles.Count == 0) continue;

                    foreach (var detector in multiAssetDetectors)
                    {
                        var results = detector.Detect(symbol, primary, corr, corrCandles, tf)
                            .Select(p => (p, ConfluenceScore: 1)).ToList();
                        var map = new Dictionary<ScannerTimeframe, List<CandleData>> { { tf, corrCandles } };
                        totalAlerts += await ProcessDetectionsAsync(userId, symbol, watchlistId,
                            results, config.MinConfluenceScore, map, existingKeys, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running SMT for {Symbol} vs {Correlated}", symbol, corr);
            }
        }

        return totalAlerts;
    }

    private static Dictionary<string, List<IctPatternType>> BuildPerAssetPatterns(
        List<WatchlistAsset> assets, List<string> symbols, List<IctPatternType> globalPatterns)
    {
        var result = new Dictionary<string, List<IctPatternType>>();
        foreach (string symbol in symbols)
        {
            var detectors = assets.Where(a => a.Symbol == symbol)
                .SelectMany(a => a.EnabledDetectors)
                .Where(d => d.IsEnabled && !d.IsDisabled)
                .Select(d => d.PatternType).Distinct().ToList();
            result[symbol] = detectors.Count > 0 ? detectors : globalPatterns;
        }
        return result;
    }

    private async Task<Dictionary<(string, ScannerTimeframe), List<CandleData>>> FetchAllCandleDataAsync(
        List<string> symbols, List<ScannerTimeframe> timeframes, CancellationToken ct)
    {
        var data = new Dictionary<(string, ScannerTimeframe), List<CandleData>>();
        foreach (string symbol in symbols)
            foreach (ScannerTimeframe tf in timeframes)
            {
                var candles = await liveDataProvider.GetRecentCandlesAsync(symbol, tf, CandlesPerTimeframe, ct);
                if (candles.Count > 0) data[(symbol, tf)] = candles;
            }
        return data;
    }

    /// <summary>
    /// Batched alert processing: dedup against prefetched data, save all alerts in one round-trip.
    /// </summary>
    private async Task<int> ProcessDetectionsAsync(
        int userId, string symbol, int watchlistId,
        List<(DetectedPattern Pattern, int ConfluenceScore)> detections,
        int minConfluenceScore,
        Dictionary<ScannerTimeframe, List<CandleData>> candlesByTf,
        HashSet<(string, IctPatternType, ScannerTimeframe)> existingKeys,
        CancellationToken ct)
    {
        List<ScannerAlert> batch = [];

        foreach (var (pattern, score) in detections.Where(d => d.ConfluenceScore >= minConfluenceScore))
        {
            if (existingKeys.Contains((symbol, pattern.Type, pattern.Timeframe))) continue;

            var candles = candlesByTf.TryGetValue(pattern.Timeframe, out var c) ? c : null;
            var regime = Indicators.MarketRegimeClassifier.Classify(candles);

            batch.Add(new ScannerAlert
            {
                Id = default!, UserId = userId, Symbol = symbol,
                PatternType = pattern.Type, Timeframe = pattern.Timeframe,
                DetectionTimeframe = pattern.Timeframe,
                PriceAtDetection = pattern.PriceAtDetection,
                ZoneHighPrice = pattern.ZoneHigh, ZoneLowPrice = pattern.ZoneLow,
                Description = pattern.Description, ConfluenceScore = score,
                Regime = regime, DetectedAt = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow, CreatedBy = userId
            });
            existingKeys.Add((symbol, pattern.Type, pattern.Timeframe));
        }

        if (batch.Count == 0) return 0;

        // Single DB round-trip for all alerts
        scannerDb.ScannerAlerts.AddRange(batch);
        await scannerDb.SaveChangesAsync(ct);

        foreach (var alert in batch)
        {
            await eventBus.PublishAsync(new ScannerAlertEvent(
                Guid.NewGuid(), userId, alert.Symbol, alert.PatternType.ToString(),
                alert.DetectionTimeframe.ToString(), alert.PriceAtDetection,
                alert.Description, alert.ConfluenceScore), ct);

            logger.LogInformation("Scanner alert: {Pattern} on {Symbol} ({Tf}) watchlist {Wl}, confluence={Score}",
                alert.PatternType, alert.Symbol, alert.DetectionTimeframe, watchlistId, alert.ConfluenceScore);
        }

        return batch.Count;
    }
}
