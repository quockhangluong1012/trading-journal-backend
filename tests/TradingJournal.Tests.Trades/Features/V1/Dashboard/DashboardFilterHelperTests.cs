using TradingJournal.Modules.Trades.Common;
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class DashboardFilterHelperTests
{
    [Fact]
    public void GetFromDate_OneDay_ReturnsYesterday()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset result = DashboardFilterHelper.GetFromDate(DashboardFilter.OneDay);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddDays(-1);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_OneWeek_ReturnsOneWeekAgo()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset result = DashboardFilterHelper.GetFromDate(DashboardFilter.OneWeek);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddDays(-7);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_OneMonth_ReturnsOneMonthAgo()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow.AddMonths(-1);
        DateTimeOffset result = DashboardFilterHelper.GetFromDate(DashboardFilter.OneMonth);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddMonths(-1);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_ThreeMonths_ReturnsThreeMonthsAgo()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow.AddMonths(-3);
        DateTimeOffset result = DashboardFilterHelper.GetFromDate(DashboardFilter.ThreeMonths);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddMonths(-3);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_AllTime_ReturnsMinValue()
    {
        DateTimeOffset result = DashboardFilterHelper.GetFromDate(DashboardFilter.AllTime);
        Assert.Equal(DateTimeOffset.MinValue, result);
    }

    [Fact]
    public void GetFromDate_InvalidFilter_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DashboardFilterHelper.GetFromDate((DashboardFilter)999));
    }
}
