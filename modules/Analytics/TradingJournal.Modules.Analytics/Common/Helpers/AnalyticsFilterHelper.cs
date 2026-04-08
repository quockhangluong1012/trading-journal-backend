namespace TradingJournal.Modules.Analytics.Common.Helpers;

public static class AnalyticsFilterHelper
{
    public static DateTime GetFromDate(AnalyticsFilter filter)
    {
        return filter switch
        {
            AnalyticsFilter.OneWeek => DateTime.UtcNow.AddDays(-7),
            AnalyticsFilter.OneMonth => DateTime.UtcNow.AddMonths(-1),
            AnalyticsFilter.ThreeMonths => DateTime.UtcNow.AddMonths(-3),
            AnalyticsFilter.SixMonths => DateTime.UtcNow.AddMonths(-6),
            AnalyticsFilter.AllTime => DateTime.MinValue,
            _ => DateTime.MinValue
        };
    }
}
