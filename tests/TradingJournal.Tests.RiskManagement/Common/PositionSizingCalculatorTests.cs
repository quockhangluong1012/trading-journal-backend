namespace TradingJournal.Tests.RiskManagement.Common;

public class PositionSizingCalculatorTests
{
    [Fact]
    public void Calculate_Returns_Expected_Risk_Units_And_Lots()
    {
        // 1% of 10,000 = 100 risk; SL distance 0.01 => 10,000 units => 0.1 lots
        PositionSizingCalculator.PositionSizeResult result =
            PositionSizingCalculator.Calculate(accountBalance: 10000m, riskPercent: 1.0m, entryPrice: 1.1000m, stopLossPrice: 1.0900m);

        Assert.Equal(100m, result.RiskAmount);
        Assert.Equal(10000m, result.Units);
        Assert.Equal(0.1m, result.Lots);
    }

    [Theory]
    [InlineData(0, 1, 1.1, 1.09)]      // zero balance
    [InlineData(10000, 0, 1.1, 1.09)]  // zero risk
    [InlineData(10000, 1, 0, 1.09)]    // zero entry
    [InlineData(10000, 1, 1.1, 0)]     // zero stop loss
    [InlineData(-10000, 1, 1.1, 1.09)] // negative balance
    public void Calculate_Returns_Zeros_When_Inputs_Are_NonPositive(decimal balance, decimal risk, decimal entry, decimal sl)
    {
        PositionSizingCalculator.PositionSizeResult result =
            PositionSizingCalculator.Calculate(balance, risk, entry, sl);

        Assert.Equal(0m, result.RiskAmount);
        Assert.Equal(0m, result.Units);
        Assert.Equal(0m, result.Lots);
    }

    [Fact]
    public void Calculate_Returns_RiskAmount_But_Zero_Size_When_Entry_Equals_StopLoss()
    {
        // SL distance is zero — sizing is undefined, but the risk amount is still meaningful.
        PositionSizingCalculator.PositionSizeResult result =
            PositionSizingCalculator.Calculate(accountBalance: 10000m, riskPercent: 2.0m, entryPrice: 1.2000m, stopLossPrice: 1.2000m);

        Assert.Equal(200m, result.RiskAmount);
        Assert.Equal(0m, result.Units);
        Assert.Equal(0m, result.Lots);
    }

    [Fact]
    public void Calculate_Uses_Absolute_StopLoss_Distance_For_Short_Trades()
    {
        // Stop above entry (short trade) — distance is taken as absolute value.
        PositionSizingCalculator.PositionSizeResult result =
            PositionSizingCalculator.Calculate(accountBalance: 5000m, riskPercent: 2.0m, entryPrice: 1.1000m, stopLossPrice: 1.1200m);

        Assert.Equal(100m, result.RiskAmount);     // 2% of 5000
        Assert.Equal(5000m, result.Units);         // 100 / 0.02
        Assert.Equal(0.05m, result.Lots);
    }
}
