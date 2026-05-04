using TradingJournal.Modules.Trades.Domain;

namespace TradingJournal.Modules.Trades.Services;

/// <summary>
/// Evaluates trading discipline rules against a new or updated trade.
/// Extracted from CreateTrade handler to follow SRP.
/// </summary>
public interface IDisciplineEvaluator
{
    /// <summary>
    /// Checks trading discipline rules (max trades per day, max consecutive losses)
    /// and sets IsRuleBroken / RuleBreakReason on the trade if violated.
    /// </summary>
    Task EvaluateAsync(TradeHistory trade, int userId, CancellationToken cancellationToken = default);
}
