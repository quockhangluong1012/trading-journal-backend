namespace TradingJournal.Modules.Backtest.Services;

internal static class CsvCandleDeduplicator
{
    public static List<OhlcvCandle> KeepOnlyNewCandles(
        IEnumerable<OhlcvCandle> parsedCandles,
        ISet<DateTime> existingTimestamps)
    {
        HashSet<DateTime> seenTimestamps = existingTimestamps.ToHashSet();
        List<OhlcvCandle> newCandles = [];

        foreach (OhlcvCandle candle in parsedCandles)
        {
            if (seenTimestamps.Add(candle.Timestamp))
            {
                newCandles.Add(candle);
            }
        }

        return newCandles;
    }
}
