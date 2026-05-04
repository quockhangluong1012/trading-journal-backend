namespace TradingJournal.Modules.Psychology.Common.Enum;

/// <summary>
/// Defines all possible achievements/badges a trader can unlock.
/// </summary>
public enum AchievementType
{
    // ── Trade Milestones ──
    FirstTrade = 1,
    TenTrades = 2,
    HundredTrades = 3,
    ThousandTrades = 4,

    // ── Review Milestones ──
    FirstReview = 10,

    // ── Journaling Streaks ──
    WeekStreak = 20,
    MonthStreak = 21,
    QuarterStreak = 22,

    // ── Win Streaks ──
    WinStreak5 = 30,
    WinStreak10 = 31,

    // ── Karma Levels ──
    KarmaLevel5 = 40,
    KarmaLevel10 = 41,
    KarmaLevel25 = 42,

    // ── Psychology ──
    TiltMaster = 50,
    Disciplined = 51
}
