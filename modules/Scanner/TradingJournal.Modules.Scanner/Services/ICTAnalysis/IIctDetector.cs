namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Represents a single OHLCV candle for pattern detection.
/// </summary>
public record CandleData(
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

/// <summary>
/// Represents a detected ICT pattern with zone boundaries and metadata.
/// </summary>
public record DetectedPattern(
    IctPatternType Type,
    ScannerTimeframe Timeframe,
    decimal PriceAtDetection,
    decimal? ZoneHigh,
    decimal? ZoneLow,
    string Description,
    DateTimeOffset DetectedAt);

/// <summary>
/// Common interface for all ICT pattern detectors.
/// Each detector analyzes a sequence of candles and returns detected patterns.
/// </summary>
public interface IIctDetector
{
    IctPatternType PatternType { get; }

    /// <summary>
    /// Analyzes a sequence of candles and returns detected patterns.
    /// Candles should be ordered chronologically (oldest first).
    /// </summary>
    List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe);
}
