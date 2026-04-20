using TradingJournal.ApiGateWay.Extensions;

namespace TradingJournal.Tests.Auth.Configuration;

public sealed class CorsOriginNormalizerTests
{
    [Fact]
    public void Normalize_Removes_Trailing_Slash_From_Configured_Origins()
    {
        string[] result = CorsOriginNormalizer.Normalize(
        [
            "https://trading-journal-ui-rho.vercel.app/",
            "http://localhost:3000"
        ]);

        Assert.Equal(
        [
            "https://trading-journal-ui-rho.vercel.app",
            "http://localhost:3000"
        ], result);
    }

    [Fact]
    public void Normalize_Handles_Whitespace_And_Duplicate_Origins()
    {
        string[] result = CorsOriginNormalizer.Normalize(
        [
            "  https://trading-journal-ui-rho.vercel.app/  ",
            "https://trading-journal-ui-rho.vercel.app"
        ]);

        Assert.Single(result);
        Assert.Equal("https://trading-journal-ui-rho.vercel.app", result[0]);
    }

    [Fact]
    public void Normalize_Throws_When_No_Valid_Origins_Are_Configured()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CorsOriginNormalizer.Normalize([null, string.Empty, "   "]));

        Assert.Equal("At least one CORS allowed origin must be configured.", exception.Message);
    }

    [Fact]
    public void Normalize_Throws_When_Configured_Origin_Contains_A_Path()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CorsOriginNormalizer.Normalize(["https://trading-journal-ui-rho.vercel.app/login"]));

        Assert.Equal(
            "CORS origin 'https://trading-journal-ui-rho.vercel.app/login' must not include a path, query string, or fragment.",
            exception.Message);
    }
}