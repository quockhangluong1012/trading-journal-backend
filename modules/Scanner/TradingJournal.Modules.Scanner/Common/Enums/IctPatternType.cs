namespace TradingJournal.Modules.Scanner.Common.Enums;

public enum IctPatternType
{
    FVG = 1,                   // Fair Value Gap
    OrderBlock = 2,            // Order Block (bullish/bearish)
    BreakerBlock = 3,          // Breaker Block
    Liquidity = 4,             // Liquidity pool (equal highs/lows)
    LiquiditySweep = 5,        // Liquidity sweep (stop hunt)
    InversionFVG = 6,          // Inversion FVG (FVG traded through, acts as S/R)
    UnicornModel = 7,          // Breaker Block + FVG overlap
    VenomModel = 8,            // Liquidity sweep into OB containing FVG
    MitigationBlock = 9,       // Unmitigated OB that price returns to
    MarketStructureShift = 10, // Break of significant swing H/L
    ChangeOfCharacter = 11,    // First break of most recent swing
    Displacement = 12,         // Large impulsive candle(s)
    OptimalTradeEntry = 13,    // 62-79% fib retracement of impulse
    JudasSwing = 14,           // False breakout during kill-zone
    BalancedPriceRange = 15,   // Overlapping bullish + bearish FVG
    CISD = 16,                 // Change in State of Delivery
    SMTDivergence = 17         // Correlated asset divergence
}
