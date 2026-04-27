using TradingJournal.Modules.Trades.Common;
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Dashboard;

public sealed class DashboardFilterHelperTests
{
    [Fact]
    public void GetFromDate_OneDay_ReturnsYesterday()
    {
        DateTime before = DateTime.UtcNow.AddDays(-1);
        DateTime result = DashboardFilterHelper.GetFromDate(DashboardFilter.OneDay);
        DateTime after = DateTime.UtcNow.AddDays(-1);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_OneWeek_ReturnsOneWeekAgo()
    {
        DateTime before = DateTime.UtcNow.AddDays(-7);
        DateTime result = DashboardFilterHelper.GetFromDate(DashboardFilter.OneWeek);
        DateTime after = DateTime.UtcNow.AddDays(-7);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_OneMonth_ReturnsOneMonthAgo()
    {
        DateTime before = DateTime.UtcNow.AddMonths(-1);
        DateTime result = DashboardFilterHelper.GetFromDate(DashboardFilter.OneMonth);
        DateTime after = DateTime.UtcNow.AddMonths(-1);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_ThreeMonths_ReturnsThreeMonthsAgo()
    {
        DateTime before = DateTime.UtcNow.AddMonths(-3);
        DateTime result = DashboardFilterHelper.GetFromDate(DashboardFilter.ThreeMonths);
        DateTime after = DateTime.UtcNow.AddMonths(-3);

        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetFromDate_AllTime_ReturnsMinValue()
    {
        DateTime result = DashboardFilterHelper.GetFromDate(DashboardFilter.AllTime);
        Assert.Equal(DateTime.MinValue, result);
    }

    [Fact]
    public void GetFromDate_InvalidFilter_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DashboardFilterHelper.GetFromDate((DashboardFilter)999));
    }
}
