namespace TradingJournal.Modules.Backtest.Common.Enums;

/// <summary>
/// Defines the pip size convention for an asset class.
/// Each value maps to a specific decimal pip size used for spread calculation.
///
/// Spread in price units = DefaultSpreadPips × PipSize
/// </summary>
public enum AssetPipType
{
    /// <summary>
    /// Standard forex pairs (EUR/USD, GBP/USD, AUD/USD, etc.)
    /// 1 pip = 0.0001
    /// </summary>
    Standard = 0,

    /// <summary>
    /// JPY forex pairs (USD/JPY, EUR/JPY, GBP/JPY, etc.)
    /// 1 pip = 0.01
    /// </summary>
    JpyPair = 1,

    /// <summary>
    /// Precious metals (XAU/USD, XAG/USD, etc.)
    /// 1 pip = 0.01
    /// </summary>
    Metal = 2,

    /// <summary>
    /// Cryptocurrency pairs (BTC/USDT, ETH/USDT, etc.)
    /// 1 pip = 0.01
    /// </summary>
    Crypto = 3,

    /// <summary>
    /// Equity indices and futures (NQ, ES, YM, etc.)
    /// 1 pip = 0.25
    /// </summary>
    Index = 4,

    /// <summary>
    /// Exotic or custom assets where 1 pip = 1.0
    /// </summary>
    WholePip = 5
}

public static class AssetPipTypeExtensions
{
    /// <summary>
    /// Converts the pip type enum to its decimal pip size value.
    /// </summary>
    public static decimal ToPipSize(this AssetPipType pipType)
    {
        return pipType switch
        {
            AssetPipType.Standard => 0.0001m,
            AssetPipType.JpyPair => 0.01m,
            AssetPipType.Metal => 0.01m,
            AssetPipType.Crypto => 0.01m,
            AssetPipType.Index => 0.25m,
            AssetPipType.WholePip => 1.0m,
            _ => 0.0001m
        };
    }
}
