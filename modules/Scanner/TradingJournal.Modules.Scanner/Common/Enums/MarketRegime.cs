namespace TradingJournal.Modules.Scanner.Common.Enums;

/// <summary>
/// Classifies the current market state based on ADX, ATR, and price structure.
/// Used to provide context to scanner alerts and performance analytics.
/// </summary>
public enum MarketRegime
{
    /// <summary>
    /// ADX > 25, +DI > -DI. Strong directional upward movement.
    /// </summary>
    TrendingUp = 1,

    /// <summary>
    /// ADX > 25, -DI > +DI. Strong directional downward movement.
    /// </summary>
    TrendingDown = 2,

    /// <summary>
    /// ADX &lt; 20, ATR below median. Low volatility, sideways price action.
    /// </summary>
    RangeBound = 3,

    /// <summary>
    /// ADX &lt; 20, ATR above median. No clear direction but wide swings (choppy).
    /// </summary>
    HighVolatility = 4,

    /// <summary>
    /// ADX between 20–25. Transitional state — regime may be shifting.
    /// </summary>
    Transitional = 5
}
