using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Integration.Infrastructure;

public sealed class ValidationOnlyWebFactory : WebApplicationFactory<Program>
{
    public FakeOpenRouterAiService FakeOpenRouterAiService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:TradeDatabase",
            "Server=(localdb)\\mssqllocaldb;Database=TradingJournalValidationOnly;Trusted_Connection=True;TrustServerCertificate=True;");
        builder.UseSetting("Jwt:Secret", "IntegrationTestSecretKeyThatIs256BitsLong!!");
        builder.UseSetting("Jwt:Issuer", "TradingJournal.Tests");
        builder.UseSetting("Jwt:Audience", "TradingJournal.Tests");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IOpenRouterAIService>();
            services.AddSingleton(FakeOpenRouterAiService);
            services.AddScoped<IOpenRouterAIService>(sp => sp.GetRequiredService<FakeOpenRouterAiService>());
        });
    }

    public HttpClient CreateAuthenticatedClient(int userId = 1, string role = "User")
    {
        HttpClient client = CreateClient();
        string token = TestJwtGenerator.GenerateToken(userId, role);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}