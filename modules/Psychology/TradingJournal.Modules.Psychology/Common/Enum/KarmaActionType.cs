namespace TradingJournal.Modules.Psychology.Common.Enum;

/// <summary>
/// Defines all actions that earn or deduct karma points.
/// </summary>
public enum KarmaActionType
{
    /// <summary>Logging a new trade (+5)</summary>
    TradeJournaled = 1,

    /// <summary>Completing a trade review (+10)</summary>
    TradeReviewed = 2,

    /// <summary>Writing a psychology journal entry (+8)</summary>
    PsychologyJournalEntry = 3,

    /// <summary>Journaling every day — streak bonus (+15)</summary>
    DailyJournalingStreak = 4,

    /// <summary>Completing a weekly review wizard (+25)</summary>
    WeeklyReviewCompleted = 5,

    /// <summary>Bonus for maintaining a winning streak (+5 × streak length)</summary>
    WinStreakBonus = 6,

    /// <summary>Penalty for rule-broken trades (-10)</summary>
    RuleBrokenPenalty = 7,

    /// <summary>Recovering from high tilt back to calm (+20)</summary>
    TiltRecovery = 8,

    /// <summary>System recalculation adjustment (±N)</summary>
    SystemAdjustment = 99
}
