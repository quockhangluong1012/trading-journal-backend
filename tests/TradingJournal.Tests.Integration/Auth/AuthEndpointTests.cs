using System.Net;
using System.Text;
using System.Text.Json;
using TradingJournal.Tests.Integration.Infrastructure;

namespace TradingJournal.Tests.Integration.Auth;

/// <summary>
/// Integration tests for Authentication endpoints.
/// </summary>
public sealed class AuthEndpointTests(TradingJournalWebFactory factory)
    : IClassFixture<TradingJournalWebFactory>
{
    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = factory.CreateClient();
        var loginPayload = new { email = "nonexistent@test.com", password = "wrongpassword" };
        StringContent content = new(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/v1/auth/login", content);

        // Assert — should be 400/401 for invalid creds, not 500
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.UnprocessableEntity,
            $"Expected 400/401/422, got {response.StatusCode}");
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsError()
    {
        // Arrange
        HttpClient client = factory.CreateClient();
        var payload = new { refreshToken = "invalid-refresh-token" };
        StringContent content = new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/v1/auth/refresh-token", content);

        // Assert — should not be 500
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        HttpClient client = factory.CreateAuthenticatedClient();

        // Act — accessing a protected endpoint that should succeed with auth
        HttpResponseMessage response = await client.GetAsync("/api/v1/trades?page=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
