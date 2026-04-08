using Carter;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TradingJournal.ApiGateWay.Extensions;
using TradingJournal.Shared;
using TradingJournal.Modules.Analytics;
using TradingJournal.Modules.Trades;
using Scalar.AspNetCore;
using TradingJournal.Shared.Middlewares;
using TradingJournal.Modules.Psychology;
using TradingJournal.Modules.Auth;
using TradingJournal.Modules.Backtest;
using TradingJournal.Modules.Backtest.Hubs;
using TradingJournal.Messaging.Shared;
using System.Security.Claims;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();

ConfigurationManager configuration = builder.Configuration;

builder.Services.AddCors();

builder.Services.AddCarter();

builder.Services.AddAntiforgery();

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
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
            NameClaimType = ClaimTypes.Name,
        };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddSharedModule()
    .AddAuthModule(configuration, isDevelopment)
    .AddTradeModule(configuration, isDevelopment)
    .AddPsychologyModule(configuration, isDevelopment)
    .AddAnalyticsModule(isDevelopment)
    .AddBacktestModule(configuration, isDevelopment)
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
    await app.MigrateBacktestDatabase();
}

app.UseStaticFiles();

app.UseCustomExceptionHandler();

app.UseCors(cors => cors
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseSwaggerDoc();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapCarter();

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
});

// SignalR hub for backtest real-time communication
app.MapHub<BacktestHub>("/hubs/backtest");

await app.RunAsync();
