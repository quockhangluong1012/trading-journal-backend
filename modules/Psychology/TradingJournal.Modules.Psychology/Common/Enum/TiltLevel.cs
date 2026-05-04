namespace TradingJournal.Modules.Psychology.Common.Enum;

/// <summary>
/// Classification of a trader's tilt level based on the composite tilt score (0-100).
/// </summary>
public enum TiltLevel
{
    /// <summary>Score 0-20: Calm and focused.</summary>
    Calm = 0,

    /// <summary>Score 21-40: Slightly elevated — monitor closely.</summary>
    Elevated = 1,

    /// <summary>Score 41-60: Warning zone — consider reducing position size.</summary>
    Warning = 2,

    /// <summary>Score 61-80: High tilt — strongly consider stopping.</summary>
    High = 3,

    /// <summary>Score 81-100: Critical — circuit breaker should fire.</summary>
    Critical = 4
}
