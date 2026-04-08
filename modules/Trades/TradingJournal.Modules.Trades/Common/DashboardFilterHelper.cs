namespace TradingJournal.Modules.Trades.Common;

public static class DashboardFilterHelper
{
    public static DateTime GetFromDate(DashboardFilter filter)
    {
        return filter switch
        {
            DashboardFilter.OneDay => DateTime.UtcNow.AddDays(-1),
            DashboardFilter.OneWeek => DateTime.UtcNow.AddDays(-7),
            DashboardFilter.OneMonth => DateTime.UtcNow.AddMonths(-1),
            DashboardFilter.ThreeMonths => DateTime.UtcNow.AddMonths(-3),
            DashboardFilter.AllTime => DateTime.MinValue,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), "Invalid dashboard filter value.")
        };
    }
}
