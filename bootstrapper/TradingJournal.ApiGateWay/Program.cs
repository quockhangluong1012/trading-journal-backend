using Carter;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TradingJournal.ApiGateWay.Extensions;
using TradingJournal.Shared;
using TradingJournal.Modules.Analytics;
using TradingJournal.Modules.Trades;
using TradingJournal.Modules.AiInsights;
using Scalar.AspNetCore;
using TradingJournal.Shared.Middlewares;
using TradingJournal.Modules.Psychology;
using TradingJournal.Modules.Auth;
using TradingJournal.Modules.Backtest;

using TradingJournal.Modules.Setups;
using TradingJournal.Modules.Backtest.Hubs;
using TradingJournal.Modules.Notifications;
using TradingJournal.Modules.Notifications.Hubs;
using TradingJournal.Modules.Scanner;
using TradingJournal.Modules.Scanner.Hubs;
using TradingJournal.Messaging.Shared;
using System.Security.Claims;
using System.Net;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

const string frontendCorsPolicyName = "FrontendApp";
const string authRateLimitPolicyName = "auth";

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();

ConfigurationManager configuration = builder.Configuration;

string jwtSecret = GetRequiredConfigurationValue(configuration, "Jwt:Secret");
string jwtIssuer = GetRequiredConfigurationValue(configuration, "Jwt:Issuer");
string jwtAudience = GetRequiredConfigurationValue(configuration, "Jwt:Audience");
string[] allowedOrigins = GetAllowedOrigins(configuration);
int authPermitLimit = GetPositiveIntConfigurationValue(configuration, "RateLimiting:Auth:PermitLimit", 5);
int authWindowMinutes = GetPositiveIntConfigurationValue(configuration, "RateLimiting:Auth:WindowMinutes", 15);
int globalPermitLimit = GetPositiveIntConfigurationValue(configuration, "RateLimiting:Global:PermitLimit", 120);
int globalWindowMinutes = GetPositiveIntConfigurationValue(configuration, "RateLimiting:Global:WindowMinutes", 1);

ValidateJwtConfiguration(jwtSecret, jwtIssuer, jwtAudience);

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicyName, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.HttpContext.Response.HasStarted)
        {
            return;
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { errorCode = "TooManyRequests", message = "Too many requests. Please try again later." },
            cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"global:{GetGlobalRateLimitPartitionKey(httpContext)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPermitLimit,
                Window = TimeSpan.FromMinutes(globalWindowMinutes),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.AddPolicy(authRateLimitPolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{GetAuthRateLimitPartitionKey(httpContext)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(authWindowMinutes),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services.AddCarter();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
});

bool isDevelopment = builder.Environment.IsDevelopment();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !isDevelopment;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.Zero,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // Configure SignalR token
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services
    .AddSharedModule()
    .AddAuthModule(configuration, isDevelopment)
    .AddTradeModule(configuration, isDevelopment)
    .AddPsychologyModule(configuration, isDevelopment)
    .AddAnalyticsModule(isDevelopment)
    .AddBacktestModule(configuration, isDevelopment)

    .AddTradingSetupModule(configuration, isDevelopment)
    .AddAiInsightsModule(configuration, isDevelopment)
    .AddNotificationModule(configuration, isDevelopment)
    .AddScannerModule(configuration, isDevelopment)
    .AddInMemoryMessageQueue();

builder.Services.AddOpenApi(options =>
{
    options.UseJwtBearerAuthentication();
});

builder.Services.AddHttpContextAccessor();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.MigrateTradingDatabase();
    await app.MigratePsychologyDatabase();
    await app.MigrateBacktestDatabase();

    await app.MigrateSetupDatabase();
    await app.MigrateAiInsightsDatabase();
    await app.MigrateNotificationDatabase();
    await app.MigrateScannerDatabase();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseSecurityHeaders(app.Environment);

app.UseStaticFiles();

app.UseRouting();

app.UseCors(frontendCorsPolicyName);

app.UseCustomExceptionHandler();

app.UseRateLimiter();

app.UseSwaggerDoc();

app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();

app.MapOpenApi();

app.MapScalarApiReference(_ =>
{
});

// SignalR hub for backtest real-time communication
app.MapHub<BacktestHub>("/hubs/backtest");

// SignalR hub for real-time notification delivery
app.MapHub<NotificationHub>("/hubs/notifications");

// SignalR hub for real-time scanner alerts and status
app.MapHub<ScannerHub>("/hubs/scanner");

await app.RunAsync();

static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
{
    string? value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration value '{key}' is required.");
    }

    return value;
}

static void ValidateJwtConfiguration(string secret, string issuer, string audience)
{
    if (secret.Length < 32)
    {
        throw new InvalidOperationException("JWT Secret must be at least 32 characters long.");
    }

    if (secret.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
        issuer.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
        audience.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("JWT configuration contains unreplaced placeholder values.");
    }
}

static string[] GetAllowedOrigins(IConfiguration configuration)
{
    return CorsOriginNormalizer.Normalize(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);
}

static int GetPositiveIntConfigurationValue(IConfiguration configuration, string key, int fallback)
{
    int value = configuration.GetValue<int?>(key) ?? fallback;
    if (value <= 0)
    {
        throw new InvalidOperationException($"Configuration value '{key}' must be greater than zero.");
    }

    return value;
}

static string GetGlobalRateLimitPartitionKey(HttpContext httpContext)
{
    return GetClientIpAddress(httpContext);
}

static string GetAuthRateLimitPartitionKey(HttpContext httpContext)
{
    string remoteIp = GetClientIpAddress(httpContext);
    string method = httpContext.Request.Method;
    return $"{remoteIp}:{method}";
}

static string GetClientIpAddress(HttpContext httpContext)
{
    IPAddress? remoteIp = httpContext.Connection.RemoteIpAddress;
    if (remoteIp is not null && IsPrivateOrLoopback(remoteIp) &&
        httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
    {
        string? forwardedIp = forwardedFor.ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedIp) && IPAddress.TryParse(forwardedIp, out IPAddress? parsedForwardedIp))
        {
            return parsedForwardedIp.ToString();
        }
    }

    return remoteIp?.ToString() ?? "unknown";
}

static bool IsPrivateOrLoopback(IPAddress address)
{
    IPAddress normalizedAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    if (IPAddress.IsLoopback(normalizedAddress))
    {
        return true;
    }

    byte[] bytes = normalizedAddress.GetAddressBytes();

    if (normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    {
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    return normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
           (normalizedAddress.IsIPv6LinkLocal || normalizedAddress.IsIPv6SiteLocal || bytes[0] == 0xfc || bytes[0] == 0xfd);
}
