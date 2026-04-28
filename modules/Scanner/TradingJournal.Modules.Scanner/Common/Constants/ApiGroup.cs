namespace TradingJournal.Modules.Scanner.Common.Constants;

public static class Tags
{
    public const string Watchlists = "Scanner - Watchlists";
    public const string Alerts = "Scanner - Alerts";
    public const string Scanner = "Scanner - Control";
    public const string EconomicCalendar = "Scanner - Economic Calendar";
}

public static class ApiGroup
{
    public static class V1
    {
        internal const string Watchlists = "api/v1/scanner/watchlists";
        internal const string Alerts = "api/v1/scanner/alerts";
        internal const string Scanner = "api/v1/scanner";
        internal const string EconomicCalendar = "api/v1/scanner/economic-calendar";
    }
}
