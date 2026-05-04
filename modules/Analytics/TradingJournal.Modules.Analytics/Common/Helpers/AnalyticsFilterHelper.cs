namespace TradingJournal.Modules.Analytics.Common.Helpers;

public static class AnalyticsFilterHelper
{
    public static DateTimeOffset GetFromDate(AnalyticsFilter filter)
    {
        return filter switch
        {
            AnalyticsFilter.OneWeek => DateTimeOffset.UtcNow.AddDays(-7),
            AnalyticsFilter.OneMonth => DateTimeOffset.UtcNow.AddMonths(-1),
            AnalyticsFilter.ThreeMonths => DateTimeOffset.UtcNow.AddMonths(-3),
            AnalyticsFilter.SixMonths => DateTimeOffset.UtcNow.AddMonths(-6),
            AnalyticsFilter.AllTime => DateTimeOffset.MinValue,
            _ => DateTimeOffset.MinValue
        };
    }
}
