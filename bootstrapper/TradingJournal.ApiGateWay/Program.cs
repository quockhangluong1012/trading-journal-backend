using Carter;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TradingJournal.ApiGateWay.Extensions;
using TradingJournal.Shared;
using TradingJournal.Shared.Extensions;
using TradingJournal.Modules.Analytics;
using TradingJournal.Modules.Trades;
using TradingJournal.Modules.AiInsights;
using Scalar.AspNetCore;
using TradingJournal.Shared.Middlewares;
using TradingJournal.Modules.Psychology;
using TradingJournal.Modules.Auth;

using TradingJournal.Modules.Setups;
using TradingJournal.Modules.Notifications;
using TradingJournal.Modules.Notifications.Hubs;
using TradingJournal.Modules.Scanner;
using TradingJournal.Modules.Scanner.Hubs;
using TradingJournal.Modules.RiskManagement;
using TradingJournal.Messaging.Shared;
using System.Security.Claims;
using System.Net;
using System.Threading.RateLimiting;
using TradingJournal.Shared.Audit;
using Serilog;

// Bootstrap logger — catches any errors during startup before the full pipeline is ready.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json (replaces default logging provider)
    builder.AddSerilog();

    const string frontendCorsPolicyName = "FrontendApp";
    const string authRateLimitPolicyName = "auth";
    const string aiRateLimitPolicyName = "ai";

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwagger();

    ConfigurationManager configuration = builder.Configuration;

    string jwtSecret = configuration.GetRequiredConfigurationValue("Jwt:Secret");
    string jwtIssuer = configuration.GetRequiredConfigurationValue("Jwt:Issuer");
    string jwtAudience = configuration.GetRequiredConfigurationValue("Jwt:Audience");
    string[] allowedOrigins = configuration.GetAllowedOrigins();
    int authPermitLimit = configuration.GetPositiveIntConfigurationValue("RateLimiting:Auth:PermitLimit", 5);
    int authWindowMinutes = configuration.GetPositiveIntConfigurationValue("RateLimiting:Auth:WindowMinutes", 15);
    int aiPermitLimit = configuration.GetPositiveIntConfigurationValue("RateLimiting:Ai:PermitLimit", 10);
    int aiWindowMinutes = configuration.GetPositiveIntConfigurationValue("RateLimiting:Ai:WindowMinutes", 10);
    int globalPermitLimit = configuration.GetPositiveIntConfigurationValue("RateLimiting:Global:PermitLimit", 120);
    int globalWindowMinutes = configuration.GetPositiveIntConfigurationValue("RateLimiting:Global:WindowMinutes", 1);

    StartupConfigurationExtensions.ValidateJwtConfiguration(jwtSecret, jwtIssuer, jwtAudience);

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
                $"global:{StartupConfigurationExtensions.GetClientIpAddress(httpContext)}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPermitLimit,
                    Window = TimeSpan.FromMinutes(globalWindowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));

        options.AddPolicy(authRateLimitPolicyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                $"auth:{StartupConfigurationExtensions.GetClientIpAddress(httpContext)}:{httpContext.Request.Method}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(authWindowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));

        options.AddPolicy(aiRateLimitPolicyName, httpContext =>
        {
            string partitionKey = httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? StartupConfigurationExtensions.GetClientIpAddress(httpContext)
                : StartupConfigurationExtensions.GetClientIpAddress(httpContext);

            return RateLimitPartition.GetFixedWindowLimiter(
                $"ai:{partitionKey}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = aiPermitLimit,
                    Window = TimeSpan.FromMinutes(aiWindowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
        });
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

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

    builder.Services
        .AddSharedModule()
        .AddAuthModule(configuration, isDevelopment)
        .AddTradeModule(configuration, isDevelopment)
        .AddPsychologyModule(configuration, isDevelopment)
        .AddAnalyticsModule(isDevelopment)
        .AddTradingSetupModule(configuration, isDevelopment)
        .AddAiInsightsModule(configuration, isDevelopment)
        .AddNotificationModule(configuration, isDevelopment)
        .AddScannerModule(configuration, isDevelopment)
        .AddRiskManagementModule(configuration, isDevelopment)
        .AddInMemoryMessageQueue();

    builder.Services.AddOpenApi(options =>
    {
        options.UseJwtBearerAuthentication();
    });

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddHealthChecks()
        .AddSqlServer(configuration.GetConnectionString("TradeDatabase")!, name: "sqlserver");

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        await app.MigrateTradingDatabase();
        await app.MigratePsychologyDatabase();
        await app.MigrateSetupDatabase();
        await app.MigrateAiInsightsDatabase();
        await app.MigrateNotificationDatabase();
        await app.MigrateScannerDatabase();
        await app.MigrateRiskManagementDatabase();
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

    app.UseSerilogHttpLogging();

    app.UseIdempotency();

    app.UseRateLimiter();

    app.UseSwaggerDoc();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapCarter();

    app.MapAuditLogEndpoint();

    app.MapHealthChecks("/health");

    app.MapOpenApi();

    app.MapScalarApiReference(_ =>
    {
    });


    // SignalR hub for real-time notification delivery
    app.MapHub<NotificationHub>("/hubs/notifications");

    // SignalR hub for real-time scanner alerts and status
    app.MapHub<ScannerHub>("/hubs/scanner");

    await app.RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Marker class for WebApplicationFactory<Program> in integration tests
public partial class Program;
