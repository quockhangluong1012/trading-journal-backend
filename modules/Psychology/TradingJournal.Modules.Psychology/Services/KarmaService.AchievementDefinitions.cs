using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

internal sealed partial class KarmaService
{
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

}
