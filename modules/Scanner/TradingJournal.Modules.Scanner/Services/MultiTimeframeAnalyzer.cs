using TradingJournal.Modules.Scanner.Services.ICTAnalysis;

namespace TradingJournal.Modules.Scanner.Services;

/// <summary>
/// Analyzes candle data across multiple timeframes using ICT detectors.
/// For each symbol, runs all enabled detectors on each enabled timeframe,
/// then calculates a confluence score based on how many timeframes confirm a pattern type.
/// </summary>
internal sealed class MultiTimeframeAnalyzer(IEnumerable<IIctDetector> detectors)
{
    /// <summary>
    /// Run all enabled detectors across the provided timeframe data.
    /// Returns detected patterns with confluence scores computed.
    /// </summary>
    public List<(DetectedPattern Pattern, int ConfluenceScore)> Analyze(
        string symbol,
        Dictionary<ScannerTimeframe, List<CandleData>> candlesByTimeframe,
        IReadOnlyList<IctPatternType> enabledPatterns)
    {
        // Gather raw detections per pattern type per timeframe
        var detectionsByPatternType = new Dictionary<IctPatternType, List<(ScannerTimeframe Tf, DetectedPattern Pattern)>>();

        foreach (var (timeframe, candles) in candlesByTimeframe)
        {
            if (candles.Count == 0) continue;

            foreach (IIctDetector detector in detectors)
            {
                if (!enabledPatterns.Contains(detector.PatternType)) continue;

                List<DetectedPattern> detected = detector.Detect(candles, symbol, timeframe);

                foreach (DetectedPattern pattern in detected)
                {
                    if (!detectionsByPatternType.ContainsKey(pattern.Type))
                    {
                        detectionsByPatternType[pattern.Type] = [];
                    }

                    detectionsByPatternType[pattern.Type].Add((timeframe, pattern));
                }
            }
        }

        // Compute confluence: how many unique timeframes detected each pattern type for this symbol
        var results = new List<(DetectedPattern Pattern, int ConfluenceScore)>();

        foreach (var (patternType, detections) in detectionsByPatternType)
        {
            int confluenceScore = detections.Select(d => d.Tf).Distinct().Count();

            // Return only the most recent detection per pattern type per timeframe
            var grouped = detections
                .GroupBy(d => d.Tf)
                .Select(g => g.OrderByDescending(d => d.Pattern.DetectedAt).First());

            foreach (var detection in grouped)
            {
                results.Add((detection.Pattern, confluenceScore));
            }
        }

        return results;
    }
}
