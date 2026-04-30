namespace TradingJournal.Modules.RiskManagement.Common.Helpers;

/// <summary>
/// Calculates position size based on account balance, risk percentage, and stop loss distance.
/// Formula: Position Size = (Account Balance × Risk %) / Stop Loss Distance (in price)
/// </summary>
public static class PositionSizingCalculator
{
    /// <summary>
    /// Calculate the position size (in lots or units).
    /// </summary>
    /// <param name="accountBalance">Current account balance.</param>
    /// <param name="riskPercent">Risk per trade as percentage (e.g., 1.0 = 1%).</param>
    /// <param name="entryPrice">Entry price of the trade.</param>
    /// <param name="stopLossPrice">Stop loss price of the trade.</param>
    /// <returns>The dollar amount to risk and the position size in units.</returns>
    public static PositionSizeResult Calculate(decimal accountBalance, decimal riskPercent, decimal entryPrice, decimal stopLossPrice)
    {
        if (accountBalance <= 0 || riskPercent <= 0 || entryPrice <= 0 || stopLossPrice <= 0)
            return new PositionSizeResult(0, 0, 0);

        decimal riskAmount = accountBalance * (riskPercent / 100m);
        decimal slDistance = Math.Abs(entryPrice - stopLossPrice);

        if (slDistance == 0)
            return new PositionSizeResult(riskAmount, 0, 0);

        decimal positionSize = riskAmount / slDistance;

        // For forex (pip-based), standard lot = 100,000 units
        decimal lots = positionSize / 100_000m;

        return new PositionSizeResult(riskAmount, Math.Round(positionSize, 2), Math.Round(lots, 4));
    }

    public sealed record PositionSizeResult(decimal RiskAmount, decimal Units, decimal Lots);
}
