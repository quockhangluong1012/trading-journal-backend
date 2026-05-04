namespace TradingJournal.Modules.Psychology.Common.Enum;

/// <summary>
/// Classification of the current streak direction.
/// </summary>
public enum StreakType
{
    /// <summary>No active streak (e.g., no closed trades or last trade was breakeven).</summary>
    None = 0,

    /// <summary>Consecutive winning trades.</summary>
    Win = 1,

    /// <summary>Consecutive losing trades.</summary>
    Loss = 2
}
