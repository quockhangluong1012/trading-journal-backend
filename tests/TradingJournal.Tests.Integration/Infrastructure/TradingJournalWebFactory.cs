using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace TradingJournal.Tests.Integration.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that replaces SQL Server connection strings
/// with a Testcontainers MsSql instance. Provides isolated, real-database
/// integration testing against a disposable SQL container.
/// </summary>
public sealed class TradingJournalWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Test@Strong!Password123")
        .Build();

    public string ConnectionString => _sqlContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:TradeDatabase", ConnectionString);
        builder.UseSetting("Jwt:Secret", "IntegrationTestSecretKeyThatIs256BitsLong!!");
        builder.UseSetting("Jwt:Issuer", "TradingJournal.Tests");
        builder.UseSetting("Jwt:Audience", "TradingJournal.Tests");

        builder.ConfigureTestServices(services =>
        {
            // Add test-specific service overrides here
        });
    }

    /// <summary>
    /// Creates an authenticated HttpClient with a fake JWT containing the given userId and role.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(int userId = 1, string role = "User")
    {
        HttpClient client = CreateClient();
        string token = TestJwtGenerator.GenerateToken(userId, role);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }
}
