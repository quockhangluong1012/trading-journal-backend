using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

/// <summary>
/// Manages karma points, levels, and achievement unlocking.
/// </summary>
public interface IKarmaService
{
    /// <summary>
    /// Awards karma points for a specific action and checks for new achievements.
    /// </summary>
    Task<KarmaRecord> AwardKarmaAsync(int userId, KarmaActionType actionType, string description,
        int? referenceId = null, int? overridePoints = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the karma summary for a user (total points, level, title, recent events).
    /// </summary>
    Task<KarmaSummaryViewModel> GetKarmaSummaryAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets karma event history for a user within a date range.
    /// </summary>
    Task<List<KarmaEventViewModel>> GetKarmaHistoryAsync(int userId, int days = 30, CancellationToken ct = default);

    /// <summary>
    /// Gets all achievements with their unlock status for a user.
    /// </summary>
    Task<List<AchievementViewModel>> GetAchievementsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Recalculates karma from trade data (full rebuild from activity history).
    /// </summary>
    Task<KarmaSummaryViewModel> RecalculateKarmaAsync(int userId, CancellationToken ct = default);
}

internal sealed class KarmaService(
    IPsychologyDbContext psychologyDb,
    ITradeProvider tradeProvider,
    IEventBus eventBus,
    ILogger<KarmaService> logger) : IKarmaService
{
    // ── Karma Point Values ──────────────────────────────────────────────

    private static readonly Dictionary<KarmaActionType, int> DefaultPoints = new()
    {
        [KarmaActionType.TradeJournaled] = 5,
        [KarmaActionType.TradeReviewed] = 10,
        [KarmaActionType.PsychologyJournalEntry] = 8,
        [KarmaActionType.DailyJournalingStreak] = 15,
        [KarmaActionType.WeeklyReviewCompleted] = 25,
        [KarmaActionType.WinStreakBonus] = 5,  // Multiplied by streak length
        [KarmaActionType.RuleBrokenPenalty] = -10,
        [KarmaActionType.TiltRecovery] = 20,
        [KarmaActionType.DailyNoteWritten] = 6,
        [KarmaActionType.SystemAdjustment] = 0,
    };

    // ── Karma Levels (25 tiers) ─────────────────────────────────────────

    private static readonly (int Threshold, string Title)[] KarmaLevels =
    [
        (0, "Novice Trader"),       // 1
        (50, "Apprentice"),          // 2
        (150, "Journeyman"),         // 3
        (300, "Skilled Trader"),     // 4
        (500, "Expert"),             // 5
        (750, "Master Trader"),      // 6
        (1100, "Grandmaster"),       // 7
        (1500, "Elite"),             // 8
        (2000, "Legend"),            // 9
        (3000, "Trading Sage"),      // 10
        (4000, "Warlord"),           // 11
        (5500, "Champion"),          // 12
        (7500, "Conqueror"),         // 13
        (10000, "Titan"),            // 14
        (13000, "Overlord"),         // 15
        (16500, "Sovereign"),        // 16
        (20500, "Ascendant"),        // 17
        (25000, "Celestial"),        // 18
        (30000, "Transcendent"),     // 19
        (36000, "Mythical"),         // 20
        (43000, "Immortal"),         // 21
        (51000, "Demigod"),          // 22
        (60000, "Divine"),           // 23
        (72000, "Eternal"),          // 24
        (85000, "Trading God"),      // 25
    ];

    // ── Achievement Definitions (144 achievements) ──────────────────────

    private static readonly AchievementDefinition[] AchievementDefinitions =
    [
        // ── Trade milestones (12) ──
        new(AchievementType.FirstTrade, "First Blood", "Log your very first trade", "🎯", "Trades"),
        new(AchievementType.TenTrades, "Getting Started", "Log 10 trades", "📊", "Trades"),
        new(AchievementType.TwentyFiveTrades, "Quarter Century", "Log 25 trades", "📈", "Trades"),
        new(AchievementType.FiftyTrades, "Half Century", "Log 50 trades", "📉", "Trades"),
        new(AchievementType.HundredTrades, "Century Trader", "Log 100 trades", "💯", "Trades"),
        new(AchievementType.TwoFiftyTrades, "Seasoned Trader", "Log 250 trades", "🎪", "Trades"),
        new(AchievementType.FiveHundredTrades, "Market Veteran", "Log 500 trades", "⚔️", "Trades"),
        new(AchievementType.ThousandTrades, "Trading Machine", "Log 1,000 trades", "🏆", "Trades"),
        new(AchievementType.TwoThousandFiveHundredTrades, "War Machine", "Log 2,500 trades", "🤖", "Trades"),
        new(AchievementType.FiveThousandTrades, "Trade God", "Log 5,000 trades", "👁️", "Trades"),
        new(AchievementType.SevenThousandFiveHundredTrades, "Market Oracle", "Log 7,500 trades", "🔮", "Trades"),
        new(AchievementType.TenThousandTrades, "Eternal Trader", "Log 10,000 trades", "♾️", "Trades"),

        // ── Review milestones (8) ──
        new(AchievementType.FirstReview, "Self-Aware", "Complete your first trade review", "📝", "Reviews"),
        new(AchievementType.FiveReviews, "Curious Mind", "Complete 5 trade reviews", "🔍", "Reviews"),
        new(AchievementType.TenReviews, "Reflective Mind", "Complete 10 trade reviews", "🪞", "Reviews"),
        new(AchievementType.TwentyFiveReviews, "Analyst", "Complete 25 trade reviews", "🔬", "Reviews"),
        new(AchievementType.FiftyReviews, "Deep Thinker", "Complete 50 trade reviews", "🧐", "Reviews"),
        new(AchievementType.HundredReviews, "Review Oracle", "Complete 100 trade reviews", "📚", "Reviews"),
        new(AchievementType.TwoHundredReviews, "Trade Scientist", "Complete 200 trade reviews", "🔭", "Reviews"),
        new(AchievementType.FiveHundredReviews, "Review Grandmaster", "Complete 500 trade reviews", "🏛️", "Reviews"),

        // ── Journaling streaks (14) ──
        new(AchievementType.ThreeDayStreak, "Warming Up", "3-day journaling streak", "🌱", "Streaks"),
        new(AchievementType.FiveDayStreak, "Building Habit", "5-day journaling streak", "🌿", "Streaks"),
        new(AchievementType.WeekStreak, "Week Warrior", "7-day journaling streak", "🔥", "Streaks"),
        new(AchievementType.TwoWeekStreak, "Fortnight Force", "14-day journaling streak", "💪", "Streaks"),
        new(AchievementType.ThreeWeekStreak, "Consistency King", "21-day journaling streak", "👑", "Streaks"),
        new(AchievementType.MonthStreak, "Monthly Master", "30-day journaling streak", "⚡", "Streaks"),
        new(AchievementType.FortyFiveDayStreak, "Habit Forged", "45-day journaling streak", "🔨", "Streaks"),
        new(AchievementType.SixtyDayStreak, "Two-Month Titan", "60-day journaling streak", "🌊", "Streaks"),
        new(AchievementType.QuarterStreak, "Quarter Legend", "90-day journaling streak", "💎", "Streaks"),
        new(AchievementType.FourMonthStreak, "Relentless", "120-day journaling streak", "🦁", "Streaks"),
        new(AchievementType.HalfYearStreak, "Half-Year Hero", "180-day journaling streak", "🏔️", "Streaks"),
        new(AchievementType.EightMonthStreak, "Marathon Mind", "240-day journaling streak", "🏃", "Streaks"),
        new(AchievementType.TenMonthStreak, "Almost There", "300-day journaling streak", "🎯", "Streaks"),
        new(AchievementType.YearStreak, "Yearly Immortal", "365-day journaling streak", "🌌", "Streaks"),

        // ── Win streaks (8) ──
        new(AchievementType.WinStreak3, "Lucky Run", "3 consecutive winning trades", "🍀", "Performance"),
        new(AchievementType.WinStreak5, "Hot Hand", "5 consecutive winning trades", "🎰", "Performance"),
        new(AchievementType.WinStreak7, "On Fire", "7 consecutive winning trades", "🔥", "Performance"),
        new(AchievementType.WinStreak10, "Unstoppable", "10 consecutive winning trades", "🌟", "Performance"),
        new(AchievementType.WinStreak15, "Legendary Run", "15 consecutive winning trades", "💫", "Performance"),
        new(AchievementType.WinStreak20, "Invincible", "20 consecutive winning trades", "☄️", "Performance"),
        new(AchievementType.WinStreak25, "Mythic Streak", "25 consecutive winning trades", "🐉", "Performance"),
        new(AchievementType.WinStreak30, "Godlike", "30 consecutive winning trades", "🏅", "Performance"),

        // ── Win rate milestones (5) ──
        new(AchievementType.WinRate50, "Above Average", "Achieve 50%+ win rate (min 30 trades)", "📊", "Performance"),
        new(AchievementType.WinRate55, "Consistent Edge", "Achieve 55%+ win rate (min 50 trades)", "📈", "Performance"),
        new(AchievementType.WinRate60, "Sharp Shooter", "Achieve 60%+ win rate (min 75 trades)", "🎯", "Performance"),
        new(AchievementType.WinRate65, "Market Wizard", "Achieve 65%+ win rate (min 100 trades)", "🧙", "Performance"),
        new(AchievementType.WinRate70, "Trading Prodigy", "Achieve 70%+ win rate (min 150 trades)", "⭐", "Performance"),

        // ── Karma levels (10) ──
        new(AchievementType.KarmaLevel2, "First Steps", "Reach karma level 2", "🌱", "Karma"),
        new(AchievementType.KarmaLevel3, "Finding Rhythm", "Reach karma level 3", "🎵", "Karma"),
        new(AchievementType.KarmaLevel5, "Rising Star", "Reach karma level 5", "⭐", "Karma"),
        new(AchievementType.KarmaLevel7, "Gaining Momentum", "Reach karma level 7", "🚀", "Karma"),
        new(AchievementType.KarmaLevel10, "Moonwalker", "Reach karma level 10", "🌙", "Karma"),
        new(AchievementType.KarmaLevel12, "Orbit Breaker", "Reach karma level 12", "🛸", "Karma"),
        new(AchievementType.KarmaLevel15, "Galaxy Brain", "Reach karma level 15", "🌌", "Karma"),
        new(AchievementType.KarmaLevel18, "Nebula Walker", "Reach karma level 18", "🪐", "Karma"),
        new(AchievementType.KarmaLevel20, "Ascended", "Reach karma level 20", "🔱", "Karma"),
        new(AchievementType.KarmaLevel25, "Trading Royalty", "Reach karma level 25", "👑", "Karma"),

        // ── Psychology (16) ──
        new(AchievementType.TiltRecovery1, "First Breath", "Recover from tilt for the first time", "🌬️", "Psychology"),
        new(AchievementType.TiltRecovery3, "Steady Hands", "Recover from tilt 3 times", "🙏", "Psychology"),
        new(AchievementType.TiltMaster, "Zen Master", "Recover from tilt 5 times", "🧘", "Psychology"),
        new(AchievementType.TiltGuru, "Inner Peace", "Recover from tilt 10 times", "☮️", "Psychology"),
        new(AchievementType.TiltRecovery15, "Tilt Slayer", "Recover from tilt 15 times", "⚔️", "Psychology"),
        new(AchievementType.TiltEnlightened, "Enlightened", "Recover from tilt 25 times", "🕊️", "Psychology"),
        new(AchievementType.TiltRecovery50, "Emotion Architect", "Recover from tilt 50 times", "🏛️", "Psychology"),
        new(AchievementType.Disciplined, "Iron Discipline", "20 consecutive trades with no rule breaks", "🎖️", "Psychology"),
        new(AchievementType.Disciplined30, "Steely Resolve", "30 consecutive trades with no rule breaks", "🔩", "Psychology"),
        new(AchievementType.Disciplined50, "Steel Mind", "50 consecutive trades with no rule breaks", "⚙️", "Psychology"),
        new(AchievementType.Disciplined100, "Diamond Hands", "100 consecutive trades with no rule breaks", "💎", "Psychology"),
        new(AchievementType.Disciplined200, "Unbreakable", "200 consecutive trades with no rule breaks", "🛡️", "Psychology"),
        new(AchievementType.Disciplined300, "Fortress", "300 consecutive trades with no rule breaks", "🏰", "Psychology"),
        new(AchievementType.Disciplined500, "Absolute Zero", "500 consecutive trades with no rule breaks", "❄️", "Psychology"),
        new(AchievementType.JournalEntries5, "First Reflections", "Write 5 psychology journal entries", "📓", "Psychology"),
        new(AchievementType.JournalEntries10, "Mind Explorer", "Write 10 psychology journal entries", "🧠", "Psychology"),
        new(AchievementType.JournalEntries25, "Thought Leader", "Write 25 psychology journal entries", "💡", "Psychology"),
        new(AchievementType.JournalEntries50, "Psych Adept", "Write 50 psychology journal entries", "🔮", "Psychology"),
        new(AchievementType.JournalEntries100, "Mental Fortress", "Write 100 psychology journal entries", "🏰", "Psychology"),
        new(AchievementType.JournalEntries250, "Soul Architect", "Write 250 psychology journal entries", "🏗️", "Psychology"),
        new(AchievementType.JournalEntries500, "Consciousness Master", "Write 500 psychology journal entries", "🧬", "Psychology"),

        // ── Daily Note Preparation (7) ──
        new(AchievementType.DailyNotes3, "Pre-Game Warm-Up", "Write daily notes for 3 consecutive days", "📋", "Preparation"),
        new(AchievementType.DailyNotes7, "Weekly Planner", "Write daily notes for 7 consecutive days", "📅", "Preparation"),
        new(AchievementType.DailyNotes14, "Battle Ready", "Write daily notes for 14 consecutive days", "🎯", "Preparation"),
        new(AchievementType.DailyNotes30, "Monthly Strategist", "Write daily notes for 30 consecutive days", "🗺️", "Preparation"),
        new(AchievementType.DailyNotes60, "Preparation Master", "Write daily notes for 60 consecutive days", "📐", "Preparation"),
        new(AchievementType.DailyNotes90, "Quarterly Commander", "Write daily notes for 90 consecutive days", "🏹", "Preparation"),
        new(AchievementType.DailyNotes180, "Half-Year General", "Write daily notes for 180 consecutive days", "⚔️", "Preparation"),

        // ── Risk Management (6) ──
        new(AchievementType.RiskReward2x10, "Smart Risk", "10 trades with 2:1+ reward-to-risk", "📏", "RiskManagement"),
        new(AchievementType.RiskReward2x25, "Risk Calculator", "25 trades with 2:1+ reward-to-risk", "🧮", "RiskManagement"),
        new(AchievementType.RiskReward2x50, "Risk Architect", "50 trades with 2:1+ reward-to-risk", "📐", "RiskManagement"),
        new(AchievementType.RiskReward3x10, "Sniper's Edge", "10 trades with 3:1+ reward-to-risk", "🎯", "RiskManagement"),
        new(AchievementType.RiskReward3x25, "Precision Master", "25 trades with 3:1+ reward-to-risk", "💎", "RiskManagement"),
        new(AchievementType.RiskReward3x50, "Elite Sniper", "50 trades with 3:1+ reward-to-risk", "🏹", "RiskManagement"),

        // ── Loss Recovery (5) ──
        new(AchievementType.Recovery3, "Bounce Back", "Recover with a win after 3+ consecutive losses", "🔄", "Recovery"),
        new(AchievementType.Recovery5, "Resilient Trader", "Recover after 3+ losses, 5 times", "💪", "Recovery"),
        new(AchievementType.Recovery10, "Comeback King", "Recover after 3+ losses, 10 times", "👑", "Recovery"),
        new(AchievementType.Recovery25, "Phoenix Trader", "Recover after 3+ losses, 25 times", "🔥", "Recovery"),
        new(AchievementType.Recovery50, "Unbreakable Spirit", "Recover after 3+ losses, 50 times", "🦅", "Recovery"),

        // ── Diversification (4) ──
        new(AchievementType.Assets5, "Explorer", "Trade 5 different assets", "🗺️", "Diversification"),
        new(AchievementType.Assets10, "Globetrotter", "Trade 10 different assets", "🌍", "Diversification"),
        new(AchievementType.Assets20, "Market Explorer", "Trade 20 different assets", "🧭", "Diversification"),
        new(AchievementType.Setups5, "Versatile Trader", "Use 5 different trading setups", "🔧", "Diversification"),

        // ── ICT Methodology (20) ──
        new(AchievementType.IctPo3First, "AMD Initiate", "Log your first Power of 3 trade", "⚡", "ICT"),
        new(AchievementType.IctPo3_10, "AMD Practitioner", "Log 10 Power of 3 trades", "🔋", "ICT"),
        new(AchievementType.IctPo3_25, "AMD Specialist", "Log 25 Power of 3 trades", "💡", "ICT"),
        new(AchievementType.IctPo3_50, "AMD Master", "Log 50 Power of 3 trades", "🌩️", "ICT"),

        new(AchievementType.IctDiscountEntry5, "Discount Hunter", "5 trades entered in Discount zone", "🏷️", "ICT"),
        new(AchievementType.IctDiscountEntry25, "Value Seeker", "25 trades entered in Discount zone", "💰", "ICT"),
        new(AchievementType.IctDiscountEntry50, "Deep Value Master", "50 trades entered in Discount zone", "🏦", "ICT"),

        new(AchievementType.IctMarketStructure5, "Structure Reader", "5 trades with Market Structure tagged", "📐", "ICT"),
        new(AchievementType.IctMarketStructure25, "Structure Analyst", "25 trades with Market Structure tagged", "🏗️", "ICT"),
        new(AchievementType.IctMarketStructure50, "Structure Architect", "50 trades with Market Structure tagged", "🏛️", "ICT"),

        new(AchievementType.IctBiasAligned5, "Bias Follower", "5 trades aligned with Daily Bias", "🧭", "ICT"),
        new(AchievementType.IctBiasAligned25, "Narrative Trader", "25 trades aligned with Daily Bias", "📖", "ICT"),
        new(AchievementType.IctBiasAligned50, "HTF Disciple", "50 trades aligned with Daily Bias", "🔭", "ICT"),
        new(AchievementType.IctBiasAlignedWin10, "Bias Sniper", "10 winning trades aligned with Daily Bias", "🎯", "ICT"),

        new(AchievementType.IctKillzone10, "Killzone Rookie", "10 trades during a Killzone session", "⏰", "ICT"),
        new(AchievementType.IctKillzone50, "Killzone Warrior", "50 trades during a Killzone session", "⚔️", "ICT"),
        new(AchievementType.IctKillzone100, "Killzone Commander", "100 trades during a Killzone session", "🏰", "ICT"),

        new(AchievementType.IctComplete5, "ICT Student", "5 trades with all ICT fields completed", "📚", "ICT"),
        new(AchievementType.IctComplete25, "ICT Practitioner", "25 trades with all ICT fields completed", "🎓", "ICT"),
        new(AchievementType.IctComplete50, "ICT Master", "50 trades with all ICT fields completed", "👁️", "ICT"),

        // ── ICT Extended Mastery (15) ──
        new(AchievementType.IctPremiumEntry5, "Premium Seller", "5 trades entered in Premium zone", "💎", "ICT"),
        new(AchievementType.IctPremiumEntry25, "Premium Hunter", "25 trades entered in Premium zone", "🏷️", "ICT"),
        new(AchievementType.IctPremiumEntry50, "Premium Overlord", "50 trades entered in Premium zone", "👑", "ICT"),
        new(AchievementType.IctBosFirst, "Structure Breaker", "Log your first BOS-tagged trade", "💥", "ICT"),
        new(AchievementType.IctBos25, "BOS Specialist", "25 trades with Break of Structure", "⚡", "ICT"),
        new(AchievementType.IctChoch10, "Reversal Reader", "10 trades with CHoCH tagged", "🔄", "ICT"),
        new(AchievementType.IctChoch25, "CHoCH Master", "25 trades with Change of Character", "🌀", "ICT"),
        new(AchievementType.IctDistribution10, "Distribution Catcher", "10 trades in Distribution phase", "📤", "ICT"),
        new(AchievementType.IctDistribution25, "Distribution Expert", "25 trades in Distribution phase", "🎯", "ICT"),
        new(AchievementType.IctManipulation10, "Manipulation Spotter", "10 trades during Manipulation phase", "🕵️", "ICT"),
        new(AchievementType.IctAccumulation10, "Accumulation Reader", "10 trades during Accumulation phase", "📥", "ICT"),
        new(AchievementType.IctConfluentWin5, "Confluent Trader", "5 winning trades with full ICT confluence", "🎖️", "ICT"),
        new(AchievementType.IctConfluentWin25, "ICT Grandmaster", "25 winning trades with full ICT confluence", "🏆", "ICT"),
        new(AchievementType.IctKillzoneWin10, "Killzone Sniper", "10 winning trades during Killzone", "🎯", "ICT"),
        new(AchievementType.IctKillzoneWin25, "Killzone Dominator", "25 winning trades during Killzone", "💀", "ICT"),

        // ── Profit & Advanced R:R (13) ──
        new(AchievementType.RiskReward5x5, "Sniper Elite", "5 trades with 5:1+ reward-to-risk", "🎯", "Profit"),
        new(AchievementType.RiskReward5x25, "Precision God", "25 trades with 5:1+ reward-to-risk", "💫", "Profit"),
        new(AchievementType.RiskReward10x1, "Lottery Winner", "1 trade with 10:1+ reward-to-risk", "🎰", "Profit"),
        new(AchievementType.RiskReward10x5, "Grand Slam", "5 trades with 10:1+ reward-to-risk", "🏟️", "Profit"),
        new(AchievementType.ProfitableDay10, "Green Day Starter", "10 profitable trading days", "🌿", "Profit"),
        new(AchievementType.ProfitableDay25, "Consistent Winner", "25 profitable trading days", "🌳", "Profit"),
        new(AchievementType.ProfitableDay50, "Profit Machine", "50 profitable trading days", "💰", "Profit"),
        new(AchievementType.ProfitableDay100, "Century of Green", "100 profitable trading days", "💵", "Profit"),
        new(AchievementType.ProfitableWeek5, "Weekly Winner", "5 profitable trading weeks", "📈", "Profit"),
        new(AchievementType.ProfitableWeek10, "Quarterly Crusher", "10 profitable trading weeks", "📊", "Profit"),
        new(AchievementType.ProfitableWeek25, "Half-Year Hero", "25 profitable trading weeks", "🏅", "Profit"),
        new(AchievementType.BestTradeRR5, "Five-Bagger", "A single trade achieving 5R+ return", "⭐", "Profit"),
        new(AchievementType.BestTradeRR10, "Ten-Bagger", "A single trade achieving 10R+ return", "🌟", "Profit"),

        // ── Prop Firm Challenge (11) ──
        new(AchievementType.PropMinDays5, "Active Trader", "Trade on 5+ unique days in a month", "📅", "PropFirm"),
        new(AchievementType.PropMinDays10, "Regular Trader", "Trade on 10+ unique days in a month", "📆", "PropFirm"),
        new(AchievementType.PropMinDays20, "Full-Time Trader", "Trade on 20+ unique days in a month", "🗓️", "PropFirm"),
        new(AchievementType.PropNoDailyLoss5, "Risk Guardian", "5 consecutive trading days without a losing day", "🛡️", "PropFirm"),
        new(AchievementType.PropNoDailyLoss10, "Drawdown Slayer", "10 consecutive trading days without a losing day", "⚔️", "PropFirm"),
        new(AchievementType.PropNoDailyLoss20, "Funded Discipline", "20 consecutive trading days without a losing day", "🏦", "PropFirm"),
        new(AchievementType.PropConsistency10, "Consistency Rookie", "10 trading days passing consistency rule", "📏", "PropFirm"),
        new(AchievementType.PropConsistency30, "Consistency Master", "30 trading days passing consistency rule", "📐", "PropFirm"),
        new(AchievementType.PropPhase1, "Phase 1 Passed", "Meet Phase 1 criteria: 8%+ profit, <5% daily loss", "🥈", "PropFirm"),
        new(AchievementType.PropPhase2, "Phase 2 Passed", "Meet Phase 2 criteria: 5%+ profit, <5% daily loss", "🥇", "PropFirm"),
        new(AchievementType.PropFundedReady, "Funded Trader", "WR 55%+, Avg R:R 1.5+, 50+ trades, disciplined", "💳", "PropFirm"),

        // ── Hard / Elite (10) ──
        new(AchievementType.PerfectWeek, "Perfect Week", "All trades won in a calendar week (min 3)", "🌟", "Elite"),
        new(AchievementType.Sniper3Consecutive, "Triple Sniper", "3 consecutive winning 3:1+ R:R trades", "🎯", "Elite"),
        new(AchievementType.Sniper5Consecutive, "Penta Sniper", "5 consecutive winning 3:1+ R:R trades", "💎", "Elite"),
        new(AchievementType.IronmanTrader, "Ironman Trader", "100+ trades, 60% WR, 50+ disciplined streak", "🦾", "Elite"),
        new(AchievementType.IctSamurai, "ICT Samurai", "25 winning ICT-complete trades with 2:1+ R:R", "⚔️", "Elite"),
        new(AchievementType.ZenPerfection, "Zen Perfection", "WinStreak10 + Disciplined100 combined", "☯️", "Elite"),
        new(AchievementType.MarathonTrader, "Marathon Trader", "365-day streak + 500+ trades logged", "🏃", "Elite"),
        new(AchievementType.EliteStatus, "Elite Status", "Karma level 20+ with 60% WR and 100+ trades", "👁️", "Elite"),
        new(AchievementType.LegendaryTrader, "Legendary Trader", "1000+ trades, 55% WR, karma level 15+", "🐉", "Elite"),
        new(AchievementType.PropFirmGod, "Prop Firm God", "Phase 1 + Phase 2 passed + Funded Ready", "🔱", "Elite"),
    ];

    // ── Public API ──────────────────────────────────────────────────────

    public async Task<KarmaRecord> AwardKarmaAsync(int userId, KarmaActionType actionType, string description,
        int? referenceId = null, int? overridePoints = null, CancellationToken ct = default)
    {
        int points = overridePoints ?? DefaultPoints.GetValueOrDefault(actionType, 0);

        var record = new KarmaRecord
        {
            Id = 0,
            ActionType = actionType,
            Points = points,
            Description = description,
            ReferenceId = referenceId,
            RecordedAt = DateTimeOffset.UtcNow
        };

        psychologyDb.KarmaRecords.Add(record);
        await psychologyDb.SaveChangesAsync(ct);

        logger.LogInformation(
            "Karma awarded to user {UserId}: {ActionType} ({Points:+#;-#;0}) — {Description}",
            userId, actionType, points, description);

        // Check for new achievements after karma change
        await CheckAndUnlockAchievementsAsync(userId, ct);

        return record;
    }

    public async Task<KarmaSummaryViewModel> GetKarmaSummaryAsync(int userId, CancellationToken ct = default)
    {
        int totalKarma = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId)
            .SumAsync(k => k.Points, ct);

        // Ensure karma doesn't go negative for level calculation
        totalKarma = Math.Max(0, totalKarma);

        var (level, title, pointsToNext, nextThreshold, progress) = CalculateLevel(totalKarma);

        int unlockedAchievements = await psychologyDb.Achievements
            .AsNoTracking()
            .CountAsync(a => a.CreatedBy == userId, ct);

        // Calculate current journaling streak
        int journalingStreak = await CalculateJournalingStreakAsync(userId, ct);

        // Get recent karma events (last 10)
        var recentEvents = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId)
            .OrderByDescending(k => k.RecordedAt)
            .Take(10)
            .Select(k => new KarmaEventViewModel
            {
                ActionType = k.ActionType.ToString(),
                Points = k.Points,
                Description = k.Description,
                RecordedAt = k.RecordedAt
            })
            .ToListAsync(ct);

        return new KarmaSummaryViewModel
        {
            TotalKarma = totalKarma,
            Level = level,
            Title = title,
            PointsToNextLevel = pointsToNext,
            NextLevelThreshold = nextThreshold,
            LevelProgress = progress,
            TotalAchievements = AchievementDefinitions.Length,
            UnlockedAchievements = unlockedAchievements,
            CurrentJournalingStreak = journalingStreak,
            RecentEvents = recentEvents
        };
    }

    public async Task<List<KarmaEventViewModel>> GetKarmaHistoryAsync(int userId, int days = 30, CancellationToken ct = default)
    {
        DateTimeOffset since = DateTimeOffset.UtcNow.AddDays(-days);

        return await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.RecordedAt >= since)
            .OrderByDescending(k => k.RecordedAt)
            .Select(k => new KarmaEventViewModel
            {
                ActionType = k.ActionType.ToString(),
                Points = k.Points,
                Description = k.Description,
                RecordedAt = k.RecordedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<AchievementViewModel>> GetAchievementsAsync(int userId, CancellationToken ct = default)
    {
        var unlockedSet = await psychologyDb.Achievements
            .AsNoTracking()
            .Where(a => a.CreatedBy == userId)
            .ToDictionaryAsync(a => a.AchievementType, a => a.UnlockedAt, ct);

        return AchievementDefinitions.Select(def => new AchievementViewModel
        {
            Type = def.Type.ToString(),
            Name = def.Name,
            Description = def.Description,
            Emoji = def.Emoji,
            Category = def.Category,
            IsUnlocked = unlockedSet.ContainsKey(def.Type),
            UnlockedAt = unlockedSet.GetValueOrDefault(def.Type)
        }).ToList();
    }

    public async Task<KarmaSummaryViewModel> RecalculateKarmaAsync(int userId, CancellationToken ct = default)
    {
        // Fetch all trade data to recalculate karma from scratch
        var trades = await tradeProvider.GetTradesAsync(userId, ct);
        var closedTrades = trades.Where(t => t.ClosedDate.HasValue).ToList();

        // Clear existing karma records for a clean recalculation
        var existingRecords = await psychologyDb.KarmaRecords
            .Where(k => k.CreatedBy == userId)
            .ToListAsync(ct);

        psychologyDb.KarmaRecords.RemoveRange(existingRecords);
        await psychologyDb.SaveChangesAsync(ct);

        // Award karma for each trade
        foreach (var trade in trades)
        {
            var record = new KarmaRecord
            {
                Id = 0,
                ActionType = KarmaActionType.TradeJournaled,
                Points = DefaultPoints[KarmaActionType.TradeJournaled],
                Description = $"Trade logged: {trade.Asset}",
                ReferenceId = trade.Id,
                RecordedAt = trade.Date
            };
            psychologyDb.KarmaRecords.Add(record);
        }

        // Award karma for rule-broken trades (penalty)
        foreach (var trade in trades.Where(t => t.IsRuleBroken))
        {
            var record = new KarmaRecord
            {
                Id = 0,
                ActionType = KarmaActionType.RuleBrokenPenalty,
                Points = DefaultPoints[KarmaActionType.RuleBrokenPenalty],
                Description = $"Rule broken on trade: {trade.Asset}",
                ReferenceId = trade.Id,
                RecordedAt = trade.Date
            };
            psychologyDb.KarmaRecords.Add(record);
        }

        // Award karma for psychology journal entries
        var journalEntries = await psychologyDb.PsychologyJournals
            .AsNoTracking()
            .Where(j => j.CreatedBy == userId)
            .ToListAsync(ct);

        foreach (var entry in journalEntries)
        {
            var record = new KarmaRecord
            {
                Id = 0,
                ActionType = KarmaActionType.PsychologyJournalEntry,
                Points = DefaultPoints[KarmaActionType.PsychologyJournalEntry],
                Description = "Psychology journal entry",
                ReferenceId = entry.Id,
                RecordedAt = entry.CreatedDate
            };
            psychologyDb.KarmaRecords.Add(record);
        }

        // Calculate win streak bonuses from closed trades
        await AwardWinStreakBonusesAsync(closedTrades);

        await psychologyDb.SaveChangesAsync(ct);

        logger.LogInformation("Karma recalculated for user {UserId}: {TradeCount} trades, {JournalCount} journal entries",
            userId, trades.Count, journalEntries.Count);

        // Re-check all achievements
        await CheckAndUnlockAchievementsAsync(userId, ct);

        return await GetKarmaSummaryAsync(userId, ct);
    }

    // ── Private Helpers ─────────────────────────────────────────────────

    private static (int Level, string Title, int PointsToNext, int NextThreshold, double Progress) CalculateLevel(int totalKarma)
    {
        int level = 1;
        string title = KarmaLevels[0].Title;
        int nextThreshold = KarmaLevels.Length > 1 ? KarmaLevels[1].Threshold : int.MaxValue;
        int currentThreshold = 0;

        for (int i = KarmaLevels.Length - 1; i >= 0; i--)
        {
            if (totalKarma >= KarmaLevels[i].Threshold)
            {
                level = i + 1;
                title = KarmaLevels[i].Title;
                currentThreshold = KarmaLevels[i].Threshold;
                nextThreshold = i + 1 < KarmaLevels.Length ? KarmaLevels[i + 1].Threshold : KarmaLevels[i].Threshold;
                break;
            }
        }

        int pointsToNext = level >= KarmaLevels.Length ? 0 : nextThreshold - totalKarma;
        double progress = level >= KarmaLevels.Length
            ? 100.0
            : nextThreshold == currentThreshold
                ? 100.0
                : (double)(totalKarma - currentThreshold) / (nextThreshold - currentThreshold) * 100.0;

        return (level, title, Math.Max(0, pointsToNext), nextThreshold, Math.Clamp(progress, 0, 100));
    }

    private async Task<int> CalculateJournalingStreakAsync(int userId, CancellationToken ct)
    {
        // Get dates where user has activity (trades or journal entries)
        var tradeDates = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId &&
                   (k.ActionType == KarmaActionType.TradeJournaled ||
                    k.ActionType == KarmaActionType.PsychologyJournalEntry))
            .Select(k => k.RecordedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(365) // Look back at most 1 year
            .ToListAsync(ct);

        if (tradeDates.Count == 0)
            return 0;

        // Count consecutive days from today
        int streak = 0;
        DateTime checkDate = DateTimeOffset.UtcNow.Date;

        // Allow today or yesterday as the start of the streak
        if (!tradeDates.Contains(checkDate))
        {
            checkDate = checkDate.AddDays(-1);
            if (!tradeDates.Contains(checkDate))
                return 0;
        }

        foreach (DateTime date in tradeDates.OrderByDescending(d => d))
        {
            if (date == checkDate)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else if (date < checkDate)
            {
                break;
            }
        }

        return streak;
    }

    private async Task CheckAndUnlockAchievementsAsync(int userId, CancellationToken ct)
    {
        var existingAchievements = await psychologyDb.Achievements
            .Where(a => a.CreatedBy == userId)
            .Select(a => a.AchievementType)
            .ToListAsync(ct);

        int totalKarma = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId)
            .SumAsync(k => k.Points, ct);

        totalKarma = Math.Max(0, totalKarma);
        var (level, _, _, _, _) = CalculateLevel(totalKarma);

        // Get trade count
        int tradeCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.TradeJournaled)
            .CountAsync(ct);

        // Get review count
        int reviewCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.TradeReviewed)
            .CountAsync(ct);

        // Get journaling streak
        int journalingStreak = await CalculateJournalingStreakAsync(userId, ct);

        // Get win streak data
        var latestStreak = await psychologyDb.StreakRecords
            .AsNoTracking()
            .Where(s => s.CreatedBy == userId)
            .OrderByDescending(s => s.RecordedAt)
            .FirstOrDefaultAsync(ct);

        int bestWinStreak = latestStreak?.BestWinStreak ?? 0;

        // Get tilt recovery count
        int tiltRecoveryCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.TiltRecovery)
            .CountAsync(ct);

        // Get disciplined trade count (trades without rule breaks)
        var trades = await tradeProvider.GetTradesAsync(userId, ct);
        int consecutiveDisciplinedTrades = 0;
        foreach (var trade in trades.OrderByDescending(t => t.Date))
        {
            if (!trade.IsRuleBroken)
                consecutiveDisciplinedTrades++;
            else
                break;
        }

        // Get psychology journal entry count
        int journalEntryCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.PsychologyJournalEntry)
            .CountAsync(ct);

        // ── New: Daily note streak ──
        int dailyNoteStreak = await CalculateDailyNoteStreakAsync(userId, ct);

        // ── New: Skill metrics from trade data ──
        var closedTrades = trades.Where(t => t.ClosedDate.HasValue && t.Pnl.HasValue).ToList();
        var (rr2xCount, rr3xCount) = CalculateRiskRewardCounts(closedTrades);
        int recoveryCount = CalculateLossRecoveryCount(closedTrades);
        var (winRate, closedCount) = CalculateWinRate(closedTrades);
        int uniqueAssets = trades.Select(t => t.Asset).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int uniqueSetups = trades.Where(t => t.TradingSetupId.HasValue).Select(t => t.TradingSetupId!.Value).Distinct().Count();

        // ── New: ICT methodology metrics ──
        var ictMetrics = CalculateIctMetrics(trades, closedTrades);

        // ── New: Advanced R:R metrics ──
        var (rr5xCount, rr10xCount) = CalculateAdvancedRiskRewardCounts(closedTrades);
        var profitMetrics = CalculateProfitMetrics(closedTrades);
        var propMetrics = CalculatePropFirmMetrics(closedTrades, winRate, closedCount, consecutiveDisciplinedTrades);
        var hardMetrics = CalculateHardMetrics(closedTrades, bestWinStreak, consecutiveDisciplinedTrades, winRate, closedCount, tradeCount, level, journalingStreak, ictMetrics, existingAchievements);

        // Check each achievement
        var achievementsToUnlock = new List<(AchievementType type, AchievementDefinition def)>();

        foreach (var def in AchievementDefinitions)
        {
            if (existingAchievements.Contains(def.Type))
                continue;

            bool shouldUnlock = def.Type switch
            {
                // Trade milestones
                AchievementType.FirstTrade => tradeCount >= 1,
                AchievementType.TenTrades => tradeCount >= 10,
                AchievementType.TwentyFiveTrades => tradeCount >= 25,
                AchievementType.FiftyTrades => tradeCount >= 50,
                AchievementType.HundredTrades => tradeCount >= 100,
                AchievementType.TwoFiftyTrades => tradeCount >= 250,
                AchievementType.FiveHundredTrades => tradeCount >= 500,
                AchievementType.ThousandTrades => tradeCount >= 1000,
                AchievementType.TwoThousandFiveHundredTrades => tradeCount >= 2500,
                AchievementType.FiveThousandTrades => tradeCount >= 5000,
                AchievementType.SevenThousandFiveHundredTrades => tradeCount >= 7500,
                AchievementType.TenThousandTrades => tradeCount >= 10000,

                // Review milestones
                AchievementType.FirstReview => reviewCount >= 1,
                AchievementType.FiveReviews => reviewCount >= 5,
                AchievementType.TenReviews => reviewCount >= 10,
                AchievementType.TwentyFiveReviews => reviewCount >= 25,
                AchievementType.FiftyReviews => reviewCount >= 50,
                AchievementType.HundredReviews => reviewCount >= 100,
                AchievementType.TwoHundredReviews => reviewCount >= 200,
                AchievementType.FiveHundredReviews => reviewCount >= 500,

                // Journaling streaks
                AchievementType.ThreeDayStreak => journalingStreak >= 3,
                AchievementType.FiveDayStreak => journalingStreak >= 5,
                AchievementType.WeekStreak => journalingStreak >= 7,
                AchievementType.TwoWeekStreak => journalingStreak >= 14,
                AchievementType.ThreeWeekStreak => journalingStreak >= 21,
                AchievementType.MonthStreak => journalingStreak >= 30,
                AchievementType.FortyFiveDayStreak => journalingStreak >= 45,
                AchievementType.SixtyDayStreak => journalingStreak >= 60,
                AchievementType.QuarterStreak => journalingStreak >= 90,
                AchievementType.FourMonthStreak => journalingStreak >= 120,
                AchievementType.HalfYearStreak => journalingStreak >= 180,
                AchievementType.EightMonthStreak => journalingStreak >= 240,
                AchievementType.TenMonthStreak => journalingStreak >= 300,
                AchievementType.YearStreak => journalingStreak >= 365,

                // Win streaks
                AchievementType.WinStreak3 => bestWinStreak >= 3,
                AchievementType.WinStreak5 => bestWinStreak >= 5,
                AchievementType.WinStreak7 => bestWinStreak >= 7,
                AchievementType.WinStreak10 => bestWinStreak >= 10,
                AchievementType.WinStreak15 => bestWinStreak >= 15,
                AchievementType.WinStreak20 => bestWinStreak >= 20,
                AchievementType.WinStreak25 => bestWinStreak >= 25,
                AchievementType.WinStreak30 => bestWinStreak >= 30,

                // Karma levels
                AchievementType.KarmaLevel2 => level >= 2,
                AchievementType.KarmaLevel3 => level >= 3,
                AchievementType.KarmaLevel5 => level >= 5,
                AchievementType.KarmaLevel7 => level >= 7,
                AchievementType.KarmaLevel10 => level >= 10,
                AchievementType.KarmaLevel12 => level >= 12,
                AchievementType.KarmaLevel15 => level >= 15,
                AchievementType.KarmaLevel18 => level >= 18,
                AchievementType.KarmaLevel20 => level >= 20,
                AchievementType.KarmaLevel25 => level >= 25,

                // Psychology — tilt
                AchievementType.TiltRecovery1 => tiltRecoveryCount >= 1,
                AchievementType.TiltRecovery3 => tiltRecoveryCount >= 3,
                AchievementType.TiltMaster => tiltRecoveryCount >= 5,
                AchievementType.TiltGuru => tiltRecoveryCount >= 10,
                AchievementType.TiltRecovery15 => tiltRecoveryCount >= 15,
                AchievementType.TiltEnlightened => tiltRecoveryCount >= 25,
                AchievementType.TiltRecovery50 => tiltRecoveryCount >= 50,

                // Psychology — discipline
                AchievementType.Disciplined => consecutiveDisciplinedTrades >= 20,
                AchievementType.Disciplined30 => consecutiveDisciplinedTrades >= 30,
                AchievementType.Disciplined50 => consecutiveDisciplinedTrades >= 50,
                AchievementType.Disciplined100 => consecutiveDisciplinedTrades >= 100,
                AchievementType.Disciplined200 => consecutiveDisciplinedTrades >= 200,
                AchievementType.Disciplined300 => consecutiveDisciplinedTrades >= 300,
                AchievementType.Disciplined500 => consecutiveDisciplinedTrades >= 500,

                // Psychology — journal entries
                AchievementType.JournalEntries5 => journalEntryCount >= 5,
                AchievementType.JournalEntries10 => journalEntryCount >= 10,
                AchievementType.JournalEntries25 => journalEntryCount >= 25,
                AchievementType.JournalEntries50 => journalEntryCount >= 50,
                AchievementType.JournalEntries100 => journalEntryCount >= 100,
                AchievementType.JournalEntries250 => journalEntryCount >= 250,
                AchievementType.JournalEntries500 => journalEntryCount >= 500,

                // Daily note preparation
                AchievementType.DailyNotes3 => dailyNoteStreak >= 3,
                AchievementType.DailyNotes7 => dailyNoteStreak >= 7,
                AchievementType.DailyNotes14 => dailyNoteStreak >= 14,
                AchievementType.DailyNotes30 => dailyNoteStreak >= 30,
                AchievementType.DailyNotes60 => dailyNoteStreak >= 60,
                AchievementType.DailyNotes90 => dailyNoteStreak >= 90,
                AchievementType.DailyNotes180 => dailyNoteStreak >= 180,

                // Risk management (R:R ratio)
                AchievementType.RiskReward2x10 => rr2xCount >= 10,
                AchievementType.RiskReward2x25 => rr2xCount >= 25,
                AchievementType.RiskReward2x50 => rr2xCount >= 50,
                AchievementType.RiskReward3x10 => rr3xCount >= 10,
                AchievementType.RiskReward3x25 => rr3xCount >= 25,
                AchievementType.RiskReward3x50 => rr3xCount >= 50,

                // Loss recovery
                AchievementType.Recovery3 => recoveryCount >= 1,
                AchievementType.Recovery5 => recoveryCount >= 5,
                AchievementType.Recovery10 => recoveryCount >= 10,
                AchievementType.Recovery25 => recoveryCount >= 25,
                AchievementType.Recovery50 => recoveryCount >= 50,

                // Win rate milestones (require minimum sample size)
                AchievementType.WinRate50 => closedCount >= 30 && winRate >= 50.0,
                AchievementType.WinRate55 => closedCount >= 50 && winRate >= 55.0,
                AchievementType.WinRate60 => closedCount >= 75 && winRate >= 60.0,
                AchievementType.WinRate65 => closedCount >= 100 && winRate >= 65.0,
                AchievementType.WinRate70 => closedCount >= 150 && winRate >= 70.0,

                // Diversification
                AchievementType.Assets5 => uniqueAssets >= 5,
                AchievementType.Assets10 => uniqueAssets >= 10,
                AchievementType.Assets20 => uniqueAssets >= 20,
                AchievementType.Setups5 => uniqueSetups >= 5,

                // ICT Methodology
                AchievementType.IctPo3First => ictMetrics.Po3Count >= 1,
                AchievementType.IctPo3_10 => ictMetrics.Po3Count >= 10,
                AchievementType.IctPo3_25 => ictMetrics.Po3Count >= 25,
                AchievementType.IctPo3_50 => ictMetrics.Po3Count >= 50,

                AchievementType.IctDiscountEntry5 => ictMetrics.DiscountEntryCount >= 5,
                AchievementType.IctDiscountEntry25 => ictMetrics.DiscountEntryCount >= 25,
                AchievementType.IctDiscountEntry50 => ictMetrics.DiscountEntryCount >= 50,

                AchievementType.IctMarketStructure5 => ictMetrics.MarketStructureCount >= 5,
                AchievementType.IctMarketStructure25 => ictMetrics.MarketStructureCount >= 25,
                AchievementType.IctMarketStructure50 => ictMetrics.MarketStructureCount >= 50,

                AchievementType.IctBiasAligned5 => ictMetrics.BiasAlignedCount >= 5,
                AchievementType.IctBiasAligned25 => ictMetrics.BiasAlignedCount >= 25,
                AchievementType.IctBiasAligned50 => ictMetrics.BiasAlignedCount >= 50,
                AchievementType.IctBiasAlignedWin10 => ictMetrics.BiasAlignedWinCount >= 10,

                AchievementType.IctKillzone10 => ictMetrics.KillzoneCount >= 10,
                AchievementType.IctKillzone50 => ictMetrics.KillzoneCount >= 50,
                AchievementType.IctKillzone100 => ictMetrics.KillzoneCount >= 100,

                AchievementType.IctComplete5 => ictMetrics.CompleteIctCount >= 5,
                AchievementType.IctComplete25 => ictMetrics.CompleteIctCount >= 25,
                AchievementType.IctComplete50 => ictMetrics.CompleteIctCount >= 50,

                // ICT Extended Mastery
                AchievementType.IctPremiumEntry5 => ictMetrics.PremiumEntryCount >= 5,
                AchievementType.IctPremiumEntry25 => ictMetrics.PremiumEntryCount >= 25,
                AchievementType.IctPremiumEntry50 => ictMetrics.PremiumEntryCount >= 50,
                AchievementType.IctBosFirst => ictMetrics.BosCount >= 1,
                AchievementType.IctBos25 => ictMetrics.BosCount >= 25,
                AchievementType.IctChoch10 => ictMetrics.ChochCount >= 10,
                AchievementType.IctChoch25 => ictMetrics.ChochCount >= 25,
                AchievementType.IctDistribution10 => ictMetrics.DistributionCount >= 10,
                AchievementType.IctDistribution25 => ictMetrics.DistributionCount >= 25,
                AchievementType.IctManipulation10 => ictMetrics.ManipulationCount >= 10,
                AchievementType.IctAccumulation10 => ictMetrics.AccumulationCount >= 10,
                AchievementType.IctConfluentWin5 => ictMetrics.ConfluentWinCount >= 5,
                AchievementType.IctConfluentWin25 => ictMetrics.ConfluentWinCount >= 25,
                AchievementType.IctKillzoneWin10 => ictMetrics.KillzoneWinCount >= 10,
                AchievementType.IctKillzoneWin25 => ictMetrics.KillzoneWinCount >= 25,

                // Profit & Advanced R:R
                AchievementType.RiskReward5x5 => rr5xCount >= 5,
                AchievementType.RiskReward5x25 => rr5xCount >= 25,
                AchievementType.RiskReward10x1 => rr10xCount >= 1,
                AchievementType.RiskReward10x5 => rr10xCount >= 5,
                AchievementType.ProfitableDay10 => profitMetrics.ProfitableDays >= 10,
                AchievementType.ProfitableDay25 => profitMetrics.ProfitableDays >= 25,
                AchievementType.ProfitableDay50 => profitMetrics.ProfitableDays >= 50,
                AchievementType.ProfitableDay100 => profitMetrics.ProfitableDays >= 100,
                AchievementType.ProfitableWeek5 => profitMetrics.ProfitableWeeks >= 5,
                AchievementType.ProfitableWeek10 => profitMetrics.ProfitableWeeks >= 10,
                AchievementType.ProfitableWeek25 => profitMetrics.ProfitableWeeks >= 25,
                AchievementType.BestTradeRR5 => profitMetrics.BestTradeRR >= 5.0,
                AchievementType.BestTradeRR10 => profitMetrics.BestTradeRR >= 10.0,

                // Prop Firm Challenge
                AchievementType.PropMinDays5 => propMetrics.BestMonthTradingDays >= 5,
                AchievementType.PropMinDays10 => propMetrics.BestMonthTradingDays >= 10,
                AchievementType.PropMinDays20 => propMetrics.BestMonthTradingDays >= 20,
                AchievementType.PropNoDailyLoss5 => propMetrics.MaxNoDailyLossStreak >= 5,
                AchievementType.PropNoDailyLoss10 => propMetrics.MaxNoDailyLossStreak >= 10,
                AchievementType.PropNoDailyLoss20 => propMetrics.MaxNoDailyLossStreak >= 20,
                AchievementType.PropConsistency10 => propMetrics.ConsistencyDays >= 10,
                AchievementType.PropConsistency30 => propMetrics.ConsistencyDays >= 30,
                AchievementType.PropPhase1 => propMetrics.Phase1Passed,
                AchievementType.PropPhase2 => propMetrics.Phase2Passed,
                AchievementType.PropFundedReady => propMetrics.FundedReady,

                // Hard / Elite
                AchievementType.PerfectWeek => hardMetrics.HasPerfectWeek,
                AchievementType.Sniper3Consecutive => hardMetrics.MaxConsecutiveSniperRR3 >= 3,
                AchievementType.Sniper5Consecutive => hardMetrics.MaxConsecutiveSniperRR3 >= 5,
                AchievementType.IronmanTrader => hardMetrics.IsIronman,
                AchievementType.IctSamurai => hardMetrics.IsIctSamurai,
                AchievementType.ZenPerfection => hardMetrics.IsZenPerfection,
                AchievementType.MarathonTrader => hardMetrics.IsMarathonTrader,
                AchievementType.EliteStatus => hardMetrics.IsEliteStatus,
                AchievementType.LegendaryTrader => hardMetrics.IsLegendaryTrader,
                AchievementType.PropFirmGod => hardMetrics.IsPropFirmGod,

                _ => false
            };

            if (shouldUnlock)
            {
                achievementsToUnlock.Add((def.Type, def));
            }
        }

        // Persist unlocked achievements and fire events
        foreach (var (type, def) in achievementsToUnlock)
        {
            var achievement = new Achievement
            {
                Id = 0,
                AchievementType = type,
                UnlockedAt = DateTimeOffset.UtcNow
            };

            psychologyDb.Achievements.Add(achievement);
            await psychologyDb.SaveChangesAsync(ct);

            logger.LogInformation("🏆 Achievement unlocked for user {UserId}: {AchievementName} {Emoji}",
                userId, def.Name, def.Emoji);

            await eventBus.PublishAsync(new KarmaAchievementEvent(
                EventId: Guid.NewGuid(),
                UserId: userId,
                AchievementName: def.Name,
                AchievementDescription: def.Description,
                Emoji: def.Emoji,
                TotalKarma: totalKarma,
                KarmaLevel: level,
                KarmaTitle: CalculateLevel(totalKarma).Title), ct);
        }
    }

    private async Task AwardWinStreakBonusesAsync(List<TradeCacheDto> closedTrades)
    {
        if (closedTrades.Count == 0) return;

        // Walk chronologically and find streak transitions
        var orderedTrades = closedTrades.OrderBy(t => t.ClosedDate ?? t.Date).ToList();
        int currentWinStreak = 0;

        foreach (var trade in orderedTrades)
        {
            if (!trade.Pnl.HasValue) continue;

            if (trade.Pnl.Value > 0)
            {
                currentWinStreak++;

                // Award streak bonus at milestones (every 5 consecutive wins)
                if (currentWinStreak > 0 && currentWinStreak % 5 == 0)
                {
                    int bonusPoints = DefaultPoints[KarmaActionType.WinStreakBonus] * currentWinStreak;
                    var record = new KarmaRecord
                    {
                        Id = 0,
                        ActionType = KarmaActionType.WinStreakBonus,
                        Points = bonusPoints,
                        Description = $"Win streak bonus: {currentWinStreak} consecutive wins",
                        ReferenceId = trade.Id,
                        RecordedAt = trade.ClosedDate ?? trade.Date
                    };
                    psychologyDb.KarmaRecords.Add(record);
                }
            }
            else
            {
                currentWinStreak = 0;
            }
        }
    }

    /// <summary>
    /// Calculates consecutive days with daily notes, counting backward from today.
    /// </summary>
    private async Task<int> CalculateDailyNoteStreakAsync(int userId, CancellationToken ct)
    {
        var noteDates = await psychologyDb.DailyNotes
            .AsNoTracking()
            .Where(n => n.CreatedBy == userId)
            .Select(n => n.NoteDate)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(365)
            .ToListAsync(ct);

        if (noteDates.Count == 0)
            return 0;

        int streak = 0;
        var checkDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);

        // Allow today or yesterday as the start
        if (!noteDates.Contains(checkDate))
        {
            checkDate = checkDate.AddDays(-1);
            if (!noteDates.Contains(checkDate))
                return 0;
        }

        foreach (var date in noteDates.OrderByDescending(d => d))
        {
            if (date == checkDate)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else if (date < checkDate)
            {
                break;
            }
        }

        return streak;
    }

    /// <summary>
    /// Counts trades with reward-to-risk ratio >= 2:1 and >= 3:1.
    /// R:R is calculated as (TargetTier1 - Entry) / (Entry - StopLoss) for longs, inverted for shorts.
    /// </summary>
    private static (int Rr2xCount, int Rr3xCount) CalculateRiskRewardCounts(List<TradeCacheDto> closedTrades)
    {
        int rr2x = 0, rr3x = 0;

        foreach (var trade in closedTrades)
        {
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) continue;

            decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);
            decimal ratio = reward / risk;

            if (ratio >= 2m) rr2x++;
            if (ratio >= 3m) rr3x++;
        }

        return (rr2x, rr3x);
    }

    /// <summary>
    /// Counts the number of times the trader recovered with a win after 3+ consecutive losses.
    /// </summary>
    private static int CalculateLossRecoveryCount(List<TradeCacheDto> closedTrades)
    {
        var ordered = closedTrades.OrderBy(t => t.ClosedDate ?? t.Date).ToList();
        int recoveries = 0;
        int lossStreak = 0;

        foreach (var trade in ordered)
        {
            if (!trade.Pnl.HasValue) continue;

            if (trade.Pnl.Value <= 0)
            {
                lossStreak++;
            }
            else
            {
                if (lossStreak >= 3)
                    recoveries++;
                lossStreak = 0;
            }
        }

        return recoveries;
    }

    /// <summary>
    /// Calculates the overall win rate from closed trades.
    /// </summary>
    private static (double WinRate, int ClosedCount) CalculateWinRate(List<TradeCacheDto> closedTrades)
    {
        if (closedTrades.Count == 0)
            return (0, 0);

        int wins = closedTrades.Count(t => t.Pnl.HasValue && t.Pnl.Value > 0);
        double winRate = (double)wins / closedTrades.Count * 100.0;
        return (winRate, closedTrades.Count);
    }

    /// <summary>
    /// Calculates all ICT methodology metrics from trade data.
    /// </summary>
    private static IctMetricsResult CalculateIctMetrics(List<TradeCacheDto> allTrades, List<TradeCacheDto> closedTrades)
    {
        // PO3 (AMD): trades with PowerOf3Phase set
        int po3Count = allTrades.Count(t => t.PowerOf3Phase.HasValue);

        // Discount entries: PremiumDiscount == 1 (Discount)
        int discountCount = allTrades.Count(t => t.PremiumDiscount == 1);

        // Premium entries: PremiumDiscount == 0 (Premium)
        int premiumCount = allTrades.Count(t => t.PremiumDiscount == 0);

        // Market structure: trades with MarketStructure set
        int msCount = allTrades.Count(t => t.MarketStructure.HasValue);

        // Specific market structure types
        int bosCount = allTrades.Count(t => t.MarketStructure == 0); // BOS
        int chochCount = allTrades.Count(t => t.MarketStructure == 1); // CHoCH

        // Specific PO3 phases
        int accumulationCount = allTrades.Count(t => t.PowerOf3Phase == 0);
        int manipulationCount = allTrades.Count(t => t.PowerOf3Phase == 1);
        int distributionCount = allTrades.Count(t => t.PowerOf3Phase == 2);

        // Bias aligned: trade direction matches DailyBias
        int biasAligned = allTrades.Count(t =>
            t.DailyBias.HasValue &&
            ((t.DailyBias == 0 && (int)t.Position == 0) ||
             (t.DailyBias == 1 && (int)t.Position == 1)));

        int biasAlignedWins = closedTrades.Count(t =>
            t.DailyBias.HasValue &&
            t.Pnl.HasValue && t.Pnl.Value > 0 &&
            ((t.DailyBias == 0 && (int)t.Position == 0) ||
             (t.DailyBias == 1 && (int)t.Position == 1)));

        // Killzone: trades with a TradingZoneId assigned
        int killzoneCount = allTrades.Count(t => t.TradingZoneId.HasValue);

        // Killzone wins: winning trades in killzone
        int killzoneWinCount = closedTrades.Count(t =>
            t.TradingZoneId.HasValue && t.Pnl.HasValue && t.Pnl.Value > 0);

        // Complete ICT: trades with ALL 4 ICT fields filled
        int completeCount = allTrades.Count(t =>
            t.PowerOf3Phase.HasValue &&
            t.DailyBias.HasValue &&
            t.MarketStructure.HasValue &&
            t.PremiumDiscount.HasValue);

        // Confluent wins: complete ICT + winning + bias aligned
        int confluentWinCount = closedTrades.Count(t =>
            t.PowerOf3Phase.HasValue &&
            t.DailyBias.HasValue &&
            t.MarketStructure.HasValue &&
            t.PremiumDiscount.HasValue &&
            t.Pnl.HasValue && t.Pnl.Value > 0 &&
            ((t.DailyBias == 0 && (int)t.Position == 0) ||
             (t.DailyBias == 1 && (int)t.Position == 1)));

        return new IctMetricsResult(
            po3Count, discountCount, premiumCount, msCount,
            bosCount, chochCount,
            accumulationCount, manipulationCount, distributionCount,
            biasAligned, biasAlignedWins,
            killzoneCount, killzoneWinCount,
            completeCount, confluentWinCount);
    }

    private sealed record IctMetricsResult(
        int Po3Count,
        int DiscountEntryCount,
        int PremiumEntryCount,
        int MarketStructureCount,
        int BosCount,
        int ChochCount,
        int AccumulationCount,
        int ManipulationCount,
        int DistributionCount,
        int BiasAlignedCount,
        int BiasAlignedWinCount,
        int KillzoneCount,
        int KillzoneWinCount,
        int CompleteIctCount,
        int ConfluentWinCount);

    /// <summary>
    /// Counts trades with R:R >= 5:1 and >= 10:1.
    /// </summary>
    private static (int Rr5xCount, int Rr10xCount) CalculateAdvancedRiskRewardCounts(List<TradeCacheDto> closedTrades)
    {
        int rr5x = 0, rr10x = 0;
        foreach (var trade in closedTrades)
        {
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) continue;
            decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);
            decimal ratio = reward / risk;
            if (ratio >= 5m) rr5x++;
            if (ratio >= 10m) rr10x++;
        }
        return (rr5x, rr10x);
    }

    /// <summary>
    /// Calculates profit-related metrics: profitable days, weeks, best single trade R.
    /// </summary>
    private static ProfitMetricsResult CalculateProfitMetrics(List<TradeCacheDto> closedTrades)
    {
        if (closedTrades.Count == 0)
            return new ProfitMetricsResult(0, 0, 0.0);

        // Profitable days: group by close date, sum PnL per day
        int profitableDays = closedTrades
            .GroupBy(t => (t.ClosedDate ?? t.Date).Date)
            .Count(g => g.Sum(t => t.Pnl ?? 0) > 0);

        // Profitable weeks: group by ISO week
        int profitableWeeks = closedTrades
            .GroupBy(t =>
            {
                var d = (t.ClosedDate ?? t.Date).Date;
                int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                return d.AddDays(-diff); // Monday of that week
            })
            .Count(g => g.Sum(t => t.Pnl ?? 0) > 0);

        // Best single trade achieved R:R
        double bestRR = 0;
        foreach (var trade in closedTrades.Where(t => t.Pnl > 0 && t.ExitPrice.HasValue))
        {
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) continue;
            decimal actualMove = Math.Abs(trade.ExitPrice!.Value - trade.EntryPrice);
            double achievedR = (double)(actualMove / risk);
            if (achievedR > bestRR) bestRR = achievedR;
        }

        return new ProfitMetricsResult(profitableDays, profitableWeeks, bestRR);
    }

    private sealed record ProfitMetricsResult(int ProfitableDays, int ProfitableWeeks, double BestTradeRR);

    /// <summary>
    /// Calculates prop firm challenge metrics.
    /// </summary>
    private static PropFirmMetricsResult CalculatePropFirmMetrics(
        List<TradeCacheDto> closedTrades, double winRate, int closedCount, int disciplinedStreak)
    {
        if (closedTrades.Count == 0)
            return new PropFirmMetricsResult(0, 0, 0, false, false, false);

        // Best month trading days: most unique trading days in any calendar month
        int bestMonthDays = closedTrades
            .GroupBy(t => new { (t.ClosedDate ?? t.Date).Year, (t.ClosedDate ?? t.Date).Month })
            .Max(g => g.Select(t => (t.ClosedDate ?? t.Date).Date).Distinct().Count());

        // Daily PnL for consecutive no-loss-day calculation
        var dailyPnl = closedTrades
            .GroupBy(t => (t.ClosedDate ?? t.Date).Date)
            .Select(g => new { Date = g.Key, Pnl = g.Sum(t => t.Pnl ?? 0) })
            .OrderBy(d => d.Date)
            .ToList();

        // Max consecutive trading days without a losing day
        int maxNoDailyLoss = 0, currentNoDailyLoss = 0;
        foreach (var day in dailyPnl)
        {
            if (day.Pnl >= 0) { currentNoDailyLoss++; maxNoDailyLoss = Math.Max(maxNoDailyLoss, currentNoDailyLoss); }
            else { currentNoDailyLoss = 0; }
        }

        // Consistency rule: days where no single day > 40% of total period profit
        // Count days in rolling 30-day windows that pass consistency
        int consistencyDays = 0;
        if (dailyPnl.Count > 0)
        {
            decimal totalPnl = dailyPnl.Sum(d => d.Pnl);
            if (totalPnl > 0)
            {
                consistencyDays = dailyPnl.Count(d => d.Pnl <= totalPnl * 0.4m);
            }
        }

        // Phase 1: cumulative profit >= 8% equivalent (use total positive PnL ratio)
        // Simplified: at least 8 profitable days with avg R:R >= 1.5, no day losing > 5% of gains
        decimal totalProfit = dailyPnl.Where(d => d.Pnl > 0).Sum(d => d.Pnl);
        decimal totalLoss = dailyPnl.Where(d => d.Pnl < 0).Sum(d => Math.Abs(d.Pnl));
        decimal worstDay = dailyPnl.Count > 0 ? dailyPnl.Min(d => d.Pnl) : 0;

        bool phase1 = closedCount >= 20 &&
                      totalProfit > 0 &&
                      totalProfit > totalLoss &&
                      (totalLoss == 0 || Math.Abs(worstDay) < totalProfit * 0.5m) &&
                      winRate >= 45.0;

        bool phase2 = phase1 &&
                      closedCount >= 40 &&
                      winRate >= 50.0 &&
                      maxNoDailyLoss >= 5;

        // Funded ready: WR 55%+, disciplined 30+, 50+ trades
        bool fundedReady = closedCount >= 50 &&
                           winRate >= 55.0 &&
                           disciplinedStreak >= 30;

        return new PropFirmMetricsResult(bestMonthDays, maxNoDailyLoss, consistencyDays, phase1, phase2, fundedReady);
    }

    private sealed record PropFirmMetricsResult(
        int BestMonthTradingDays, int MaxNoDailyLossStreak, int ConsistencyDays,
        bool Phase1Passed, bool Phase2Passed, bool FundedReady);

    /// <summary>
    /// Calculates hard/elite achievement conditions.
    /// </summary>
    private static HardMetricsResult CalculateHardMetrics(
        List<TradeCacheDto> closedTrades, int bestWinStreak, int disciplinedStreak,
        double winRate, int closedCount, int tradeCount, int karmaLevel, int journalingStreak,
        IctMetricsResult ictMetrics, List<AchievementType> existingAchievements)
    {
        // Perfect week: any calendar week where all trades are winners (min 3)
        bool hasPerfectWeek = closedTrades
            .GroupBy(t =>
            {
                var d = (t.ClosedDate ?? t.Date).Date;
                int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                return d.AddDays(-diff);
            })
            .Any(g =>
            {
                var weekTrades = g.Where(t => t.Pnl.HasValue).ToList();
                return weekTrades.Count >= 3 && weekTrades.All(t => t.Pnl!.Value > 0);
            });

        // Max consecutive winning trades with 3:1+ R:R
        int maxSniperStreak = 0, currentSniperStreak = 0;
        foreach (var trade in closedTrades.OrderBy(t => t.ClosedDate ?? t.Date))
        {
            if (!trade.Pnl.HasValue || trade.Pnl.Value <= 0) { currentSniperStreak = 0; continue; }
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) { currentSniperStreak = 0; continue; }
            decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);
            if (reward / risk >= 3m) { currentSniperStreak++; maxSniperStreak = Math.Max(maxSniperStreak, currentSniperStreak); }
            else { currentSniperStreak = 0; }
        }

        // ICT Samurai: 25 winning ICT-complete trades with 2:1+ R:R
        int ictSamuraiCount = closedTrades.Count(t =>
            t.Pnl.HasValue && t.Pnl.Value > 0 &&
            t.PowerOf3Phase.HasValue && t.DailyBias.HasValue &&
            t.MarketStructure.HasValue && t.PremiumDiscount.HasValue &&
            Math.Abs(t.EntryPrice - t.StopLoss) > 0 &&
            Math.Abs(t.TargetTier1 - t.EntryPrice) / Math.Abs(t.EntryPrice - t.StopLoss) >= 2m);

        bool isIronman = closedCount >= 100 && winRate >= 60.0 && disciplinedStreak >= 50;
        bool isZenPerfection = bestWinStreak >= 10 && disciplinedStreak >= 100;
        bool isMarathonTrader = journalingStreak >= 365 && tradeCount >= 500;
        bool isEliteStatus = karmaLevel >= 20 && winRate >= 60.0 && closedCount >= 100;
        bool isLegendaryTrader = tradeCount >= 1000 && winRate >= 55.0 && karmaLevel >= 15;
        bool isPropFirmGod = existingAchievements.Contains(AchievementType.PropPhase1) &&
                             existingAchievements.Contains(AchievementType.PropPhase2) &&
                             existingAchievements.Contains(AchievementType.PropFundedReady);

        return new HardMetricsResult(
            hasPerfectWeek, maxSniperStreak, isIronman, ictSamuraiCount >= 25,
            isZenPerfection, isMarathonTrader, isEliteStatus, isLegendaryTrader, isPropFirmGod);
    }

    private sealed record HardMetricsResult(
        bool HasPerfectWeek, int MaxConsecutiveSniperRR3, bool IsIronman, bool IsIctSamurai,
        bool IsZenPerfection, bool IsMarathonTrader, bool IsEliteStatus,
        bool IsLegendaryTrader, bool IsPropFirmGod);

    // ── Inner Types ─────────────────────────────────────────────────────

    private sealed record AchievementDefinition(
        AchievementType Type,
        string Name,
        string Description,
        string Emoji,
        string Category);
}
