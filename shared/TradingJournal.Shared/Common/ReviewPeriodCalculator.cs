using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Common;

public static class ReviewPeriodCalculator
{
    public static ReviewPeriodBounds GetBounds(ReviewPeriodType periodType, DateTime referenceDate)
    {
        DateTime normalizedStart = periodType switch
        {
            ReviewPeriodType.Daily => referenceDate.Date,
            ReviewPeriodType.Weekly => GetStartOfWeek(referenceDate),
            ReviewPeriodType.Monthly => new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            ReviewPeriodType.Quarterly => new DateTime(referenceDate.Year, ((referenceDate.Month - 1) / 3) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => referenceDate.Date,
        };

        DateTime normalizedEnd = periodType switch
        {
            ReviewPeriodType.Daily => normalizedStart.AddDays(1).AddTicks(-1),
            ReviewPeriodType.Weekly => normalizedStart.AddDays(7).AddTicks(-1),
            ReviewPeriodType.Monthly => normalizedStart.AddMonths(1).AddTicks(-1),
            ReviewPeriodType.Quarterly => normalizedStart.AddMonths(3).AddTicks(-1),
            _ => normalizedStart.AddDays(1).AddTicks(-1),
        };

        return new ReviewPeriodBounds(normalizedStart, normalizedEnd);
    }

    private static DateTime GetStartOfWeek(DateTime referenceDate)
    {
        DateTime date = referenceDate.Date;
        int delta = date.DayOfWeek == DayOfWeek.Sunday
            ? -6
            : (int)DayOfWeek.Monday - (int)date.DayOfWeek;

        return date.AddDays(delta);
    }
}
