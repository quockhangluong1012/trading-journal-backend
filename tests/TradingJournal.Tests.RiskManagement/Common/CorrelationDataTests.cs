namespace TradingJournal.Tests.RiskManagement.Common;

public class CorrelationDataTests
{
    [Fact]
    public void GetCorrelation_Returns_One_For_Same_Asset()
    {
        Assert.Equal(1.0m, CorrelationData.GetCorrelation("EURUSD", "EURUSD"));
    }

    [Fact]
    public void GetCorrelation_Is_Case_Insensitive_For_Same_Asset()
    {
        Assert.Equal(1.0m, CorrelationData.GetCorrelation("eurusd", "EURUSD"));
    }

    [Fact]
    public void GetCorrelation_Returns_Known_Pair_Value()
    {
        Assert.Equal(0.85m, CorrelationData.GetCorrelation("EURUSD", "GBPUSD"));
    }

    [Fact]
    public void GetCorrelation_Returns_Negative_For_Inverse_Pair()
    {
        Assert.Equal(-0.92m, CorrelationData.GetCorrelation("EURUSD", "USDCHF"));
    }

    [Fact]
    public void GetCorrelation_Resolves_Reverse_Lookup_When_Pair_Defined_One_Direction()
    {
        // GBPJPY is not a top-level key, but GBPUSD defines its correlation to GBPJPY (0.72).
        Assert.Equal(0.72m, CorrelationData.GetCorrelation("GBPJPY", "GBPUSD"));
    }

    [Fact]
    public void GetCorrelation_Is_Case_Insensitive_For_Known_Pair()
    {
        Assert.Equal(0.85m, CorrelationData.GetCorrelation("eurusd", "gbpusd"));
    }

    [Fact]
    public void GetCorrelation_Returns_Zero_For_Unknown_Pair()
    {
        Assert.Equal(0.0m, CorrelationData.GetCorrelation("FOO", "BAR"));
    }

    [Fact]
    public void KnownAssets_Contains_Major_Pairs()
    {
        Assert.Contains("EURUSD", CorrelationData.KnownAssets);
        Assert.Contains("XAUUSD", CorrelationData.KnownAssets);
    }
}
