using TradingJournal.Modules.Trades.Domain;

namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Evaluates trading discipline rules against profile guardrails.
/// </summary>
internal sealed class DisciplineEvaluator(ITradeDbContext dbContext) : IDisciplineEvaluator
{
    public async Task EvaluateAsync(TradeHistory trade, int userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.TradingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CreatedBy == userId, cancellationToken);

        if (profile == null || !profile.IsDisciplineEnabled) return;

        List<string> brokenReasons = [];

        // Max Trades Per Day
        if (profile.MaxTradesPerDay.HasValue && profile.MaxTradesPerDay > 0)
        {
            var today = DateTime.UtcNow.Date;
            var tradesToday = await dbContext.TradeHistories
                .AsNoTracking()
                .CountAsync(x => x.CreatedBy == userId && x.CreatedDate >= today, cancellationToken);

            if (tradesToday >= profile.MaxTradesPerDay.Value)
            {
                brokenReasons.Add($"Exceeded max trades per day ({profile.MaxTradesPerDay.Value}).");
            }
        }

        // Max Consecutive Losses
        if (profile.MaxConsecutiveLosses.HasValue && profile.MaxConsecutiveLosses > 0)
        {
            var recentTrades = await dbContext.TradeHistories
                .AsNoTracking()
                .Where(x => x.CreatedBy == userId && x.Status == TradeStatus.Closed)
                .OrderByDescending(x => x.ClosedDate ?? x.CreatedDate)
                .Take(profile.MaxConsecutiveLosses.Value + 1)
                .ToListAsync(cancellationToken);

            int consecutiveLosses = 0;
            foreach (var recentTrade in recentTrades)
            {
                if (recentTrade.Pnl < 0 || recentTrade.TradingResult == "Loss")
                {
                    consecutiveLosses++;
                }
                else
                {
                    break;
                }
            }

            if (consecutiveLosses >= profile.MaxConsecutiveLosses.Value)
            {
                brokenReasons.Add($"Exceeded max consecutive losses ({profile.MaxConsecutiveLosses.Value}).");
            }
        }

        if (brokenReasons.Count > 0)
        {
            trade.IsRuleBroken = true;
            trade.RuleBreakReason = string.Join("; ", brokenReasons);
        }
    }
}
