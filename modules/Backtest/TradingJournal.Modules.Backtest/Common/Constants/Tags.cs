namespace TradingJournal.Modules.Backtest.Common.Constants;

public static class Tags
{
    public const string BacktestSessions = "Backtest Sessions";
    public const string BacktestOrders = "Backtest Orders";
    public const string BacktestPlayback = "Backtest Playback";
    public const string BacktestMarketData = "Backtest Market Data";
    public const string BacktestDrawings = "Backtest Drawings";
    public const string BacktestAnalytics = "Backtest Analytics";
    public const string BacktestAdmin = "Backtest Admin";
}

public static class ApiGroup
{
    public static class V1
    {
        internal const string Sessions = "api/v1/backtest-sessions";
        internal const string Orders = "api/v1/backtest-orders";
        internal const string Playback = "api/v1/backtest-playback";
        internal const string MarketData = "api/v1/backtest-market-data";
        internal const string Drawings = "api/v1/backtest-drawings";
        internal const string Analytics = "api/v1/backtest-analytics";
        internal const string Admin = "api/v1/backtest-admin/assets";
    }
}
