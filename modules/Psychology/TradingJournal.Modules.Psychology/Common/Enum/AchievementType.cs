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
    JournalEntries500 = 80,

    // ── Daily Note Preparation (110–119) ──
    DailyNotes3 = 110,
    DailyNotes7 = 111,
    DailyNotes14 = 112,
    DailyNotes30 = 113,
    DailyNotes60 = 114,
    DailyNotes90 = 115,
    DailyNotes180 = 116,

    // ── Risk Management (120–129) ──
    RiskReward2x10 = 120,
    RiskReward2x25 = 121,
    RiskReward2x50 = 122,
    RiskReward3x10 = 123,
    RiskReward3x25 = 124,
    RiskReward3x50 = 125,

    // ── Loss Recovery (130–139) ──
    Recovery3 = 130,
    Recovery5 = 131,
    Recovery10 = 132,
    Recovery25 = 133,
    Recovery50 = 134,

    // ── Win Rate Milestones (140–149) ──
    WinRate50 = 140,
    WinRate55 = 141,
    WinRate60 = 142,
    WinRate65 = 143,
    WinRate70 = 144,

    // ── Diversification (150–159) ──
    Assets5 = 150,
    Assets10 = 151,
    Assets20 = 152,
    Setups5 = 153,

    // ── ICT Methodology (160–189) ──
    // Power of 3 (AMD) trades
    IctPo3First = 160,
    IctPo3_10 = 161,
    IctPo3_25 = 162,
    IctPo3_50 = 163,

    // Discount/Premium zone entries
    IctDiscountEntry5 = 164,
    IctDiscountEntry25 = 165,
    IctDiscountEntry50 = 166,

    // Market structure trades (BOS/CHoCH tagged)
    IctMarketStructure5 = 167,
    IctMarketStructure25 = 168,
    IctMarketStructure50 = 169,

    // Daily bias alignment (trade direction matches daily bias)
    IctBiasAligned5 = 170,
    IctBiasAligned25 = 171,
    IctBiasAligned50 = 172,
    IctBiasAlignedWin10 = 173,

    // Killzone mastery (trades in specific zones)
    IctKillzone10 = 174,
    IctKillzone50 = 175,
    IctKillzone100 = 176,

    // ICT completionist (trades with ALL 4 ICT fields filled)
    IctComplete5 = 177,
    IctComplete25 = 178,
    IctComplete50 = 179,

    // ── ICT Extended Mastery (190–209) ──
    // Premium zone entries (selling from premium)
    IctPremiumEntry5 = 190,
    IctPremiumEntry25 = 191,
    IctPremiumEntry50 = 192,

    // Specific market structure: BOS
    IctBosFirst = 193,
    IctBos25 = 194,

    // Specific market structure: CHoCH (reading reversals)
    IctChoch10 = 195,
    IctChoch25 = 196,

    // Specific PO3 phases
    IctDistribution10 = 197,
    IctDistribution25 = 198,
    IctManipulation10 = 199,
    IctAccumulation10 = 200,

    // Confluent ICT wins (complete ICT + winning + bias aligned)
    IctConfluentWin5 = 201,
    IctConfluentWin25 = 202,

    // Killzone winning trades
    IctKillzoneWin10 = 203,
    IctKillzoneWin25 = 204,

    // ── Profit & Advanced R:R (210–229) ──
    RiskReward5x5 = 210,
    RiskReward5x25 = 211,
    RiskReward10x1 = 212,
    RiskReward10x5 = 213,
    ProfitableDay10 = 214,
    ProfitableDay25 = 215,
    ProfitableDay50 = 216,
    ProfitableDay100 = 217,
    ProfitableWeek5 = 218,
    ProfitableWeek10 = 219,
    ProfitableWeek25 = 220,
    BestTradeRR5 = 221,
    BestTradeRR10 = 222,

    // ── Prop Firm Challenge (240–259) ──
    PropMinDays5 = 240,
    PropMinDays10 = 241,
    PropMinDays20 = 242,
    PropNoDailyLoss5 = 243,
    PropNoDailyLoss10 = 244,
    PropNoDailyLoss20 = 245,
    PropConsistency10 = 246,
    PropConsistency30 = 247,
    PropPhase1 = 248,
    PropPhase2 = 249,
    PropFundedReady = 250,

    // ── Hard / Elite (270–289) ──
    PerfectWeek = 270,
    Sniper3Consecutive = 271,
    Sniper5Consecutive = 272,
    IronmanTrader = 273,
    IctSamurai = 274,
    ZenPerfection = 275,
    MarathonTrader = 276,
    EliteStatus = 277,
    LegendaryTrader = 278,
    PropFirmGod = 279
}
