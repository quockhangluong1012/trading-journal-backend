namespace TradingJournal.Modules.Psychology.Common.Enum;

/// <summary>
/// Defines all possible achievements/badges a trader can unlock.
/// IMPORTANT: Do NOT change existing integer values — they are persisted in the database.
/// </summary>
public enum AchievementType
{
    // ── Trade Milestones (1–9, 100–109) ──
    FirstTrade = 1,
    TenTrades = 2,
    HundredTrades = 3,
    ThousandTrades = 4,
    TwentyFiveTrades = 5,
    FiftyTrades = 6,
    TwoFiftyTrades = 7,
    FiveHundredTrades = 8,
    FiveThousandTrades = 9,
    TwoThousandFiveHundredTrades = 100,
    SevenThousandFiveHundredTrades = 101,
    TenThousandTrades = 102,

    // ── Review Milestones (10–19) ──
    FirstReview = 10,
    TenReviews = 11,
    TwentyFiveReviews = 12,
    FiftyReviews = 13,
    HundredReviews = 14,
    FiveReviews = 15,
    TwoHundredReviews = 16,
    FiveHundredReviews = 17,

    // ── Journaling Streaks (20–29, 70–79) ──
    WeekStreak = 20,
    MonthStreak = 21,
    QuarterStreak = 22,
    ThreeDayStreak = 23,
    TwoWeekStreak = 24,
    SixtyDayStreak = 25,
    HalfYearStreak = 26,
    YearStreak = 27,
    FiveDayStreak = 28,
    ThreeWeekStreak = 29,
    FortyFiveDayStreak = 70,
    FourMonthStreak = 71,
    EightMonthStreak = 72,
    TenMonthStreak = 73,

    // ── Win Streaks (30–39) ──
    WinStreak5 = 30,
    WinStreak10 = 31,
    WinStreak3 = 32,
    WinStreak7 = 33,
    WinStreak15 = 34,
    WinStreak20 = 35,
    WinStreak25 = 36,
    WinStreak30 = 37,

    // ── Karma Levels (40–49) ──
    KarmaLevel5 = 40,
    KarmaLevel10 = 41,
    KarmaLevel25 = 42,
    KarmaLevel2 = 43,
    KarmaLevel7 = 44,
    KarmaLevel15 = 45,
    KarmaLevel20 = 46,
    KarmaLevel3 = 47,
    KarmaLevel12 = 48,
    KarmaLevel18 = 49,

    // ── Psychology (50–69, 80–89) ──
    TiltMaster = 50,
    Disciplined = 51,
    TiltRecovery1 = 52,
    TiltGuru = 53,
    TiltEnlightened = 54,
    Disciplined50 = 55,
    Disciplined100 = 56,
    Disciplined200 = 57,
    JournalEntries10 = 58,
    JournalEntries50 = 59,
    JournalEntries100 = 60,
    JournalEntries250 = 61,
    TiltRecovery3 = 62,
    TiltRecovery15 = 63,
    TiltRecovery50 = 64,
    Disciplined30 = 65,
    Disciplined300 = 66,
    Disciplined500 = 67,
    JournalEntries5 = 68,
    JournalEntries25 = 69,
    JournalEntries500 = 80
}
