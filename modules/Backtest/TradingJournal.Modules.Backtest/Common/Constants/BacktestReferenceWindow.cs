namespace TradingJournal.Modules.Backtest.Common.Constants;

internal static class BacktestReferenceWindow
{
    public const int Days = 3;

    public static DateTime GetStartDate(DateTime sessionStartDate) => sessionStartDate.AddDays(-Days);
}
