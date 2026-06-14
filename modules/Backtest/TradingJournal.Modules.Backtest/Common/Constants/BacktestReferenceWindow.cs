namespace TradingJournal.Modules.Backtest.Common.Constants;

internal static class BacktestReferenceWindow
{
    public const int Days = 7;

    public static DateTime GetStartDate(DateTime sessionStartDate) => sessionStartDate.AddDays(-Days);
}
