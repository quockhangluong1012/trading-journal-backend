namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Interface for detectors that require candle data from multiple correlated assets.
/// Unlike IIctDetector (single-asset), this receives data for two symbols simultaneously.
/// </summary>
public interface IMultiAssetDetector
{
    IctPatternType PatternType { get; }

    /// <summary>
    /// Analyzes two correlated assets for divergence patterns.
    /// </summary>
    /// <param name="primarySymbol">The primary symbol being scanned.</param>
    /// <param name="primaryCandles">Candles for the primary symbol.</param>
    /// <param name="correlatedSymbol">The correlated symbol to compare against.</param>
    /// <param name="correlatedCandles">Candles for the correlated symbol.</param>
    /// <param name="timeframe">The timeframe of analysis.</param>
    List<DetectedPattern> Detect(
        string primarySymbol,
        IReadOnlyList<CandleData> primaryCandles,
        string correlatedSymbol,
        IReadOnlyList<CandleData> correlatedCandles,
        ScannerTimeframe timeframe);
}
