namespace TradingJournal.Modules.Trades.Common;

public static class DashboardFilterHelper
{
    public static DateTimeOffset GetFromDate(DashboardFilter filter)
    {
        return filter switch
        {
            DashboardFilter.OneDay => DateTimeOffset.UtcNow.AddDays(-1),
            DashboardFilter.OneWeek => DateTimeOffset.UtcNow.AddDays(-7),
            DashboardFilter.OneMonth => DateTimeOffset.UtcNow.AddMonths(-1),
            DashboardFilter.ThreeMonths => DateTimeOffset.UtcNow.AddMonths(-3),
            DashboardFilter.AllTime => DateTimeOffset.MinValue,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), "Invalid dashboard filter value.")
        };
    }
}
