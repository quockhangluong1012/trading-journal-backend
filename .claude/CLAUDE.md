# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

### Build
```bash
dotnet build bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
```
The API Gateway project references all modules, so building it compiles the entire application.

### Run
```bash
dotnet run --project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
```

### Test
Run all tests:
```bash
dotnet test tests/TradingJournal.Tests.Auth/TradingJournal.Tests.Auth.csproj
dotnet test tests/TradingJournal.Tests.Trades/TradingJournal.Tests.Trades.csproj
# Repeat for other test projects: Analytics, Backtest, Psychology, Scanner
```

Run a single test:
```bash
dotnet test tests/TradingJournal.Tests.Auth/TradingJournal.Tests.Auth.csproj --filter "FullyQualifiedName~TestMethodName"
```

### EF Core Migrations
Add migration for a module (e.g., Auth):
```bash
dotnet ef migrations add <MigrationName> \
  --project modules/Auth/TradingJournal.Modules.Auth/TradingJournal.Modules.Auth.csproj \
  --startup-project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
```

Apply migrations:
```bash
dotnet ef database update \
  --project modules/Auth/TradingJournal.Modules.Auth/TradingJournal.Modules.Auth.csproj \
  --startup-project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
```

### Format/Lint
```bash
dotnet format
```
Code analyzers (Microsoft.CodeAnalysis.Analyzers) are enabled project-wide.

## Architecture

### Modular Monolith
The application is a modular monolith with a single entry point (`TradingJournal.ApiGateWay`) that references all business modules. Each module is a separate class library with its own DbContext and EF Core migrations.

### Project Structure
- **bootstrapper/TradingJournal.ApiGateWay** - ASP.NET Core entry point, references all modules, configures auth (JWT + Google), CORS, rate limiting, Swagger/Scalar docs, OpenTelemetry.
- **modules/** - Business modules, each with its own DbContext:
  - `Auth` - User authentication, BCrypt hashing, JWT issuance
  - `Trades` - Trade history, technical analysis, AI summaries, review wizard, trade templates
  - `AiInsights` - AI-powered insights using Google Generative AI
  - `Psychology` - Trading psychology tracking
  - `Backtest` - Strategy backtesting with CSV import
  - `TradingSetup` - Trading setup/playbook management
  - `Notifications` - Notification system
  - `Scanner` - Market scanner with watchlist, ICT detectors
  - `RiskManagement` - Risk rules and guardrails
  - `Analytics` - Trading analytics
- **shared/** - Shared libraries:
  - `TradingJournal.Shared` - Common domain abstractions, base DbContext (AuditableDbContext)
  - `TradingJournal.Messaging.Shared` - MediatR contracts for in-process CQRS messaging
- **tests/** - xUnit test projects mirroring each module, using Moq for mocking.

### Key Patterns
- **Routing**: Carter minimal APIs (no traditional controllers)
- **CQRS**: MediatR for command/query separation within modules
- **Data Access**: EF Core 10.0.6 with SQL Server, each module has isolated DbContext
- **Mapping**: Mapster for DTO/domain mappings
- **Auth**: JWT Bearer tokens + Google OAuth, configured in API Gateway
- **Configuration**: `appsettings.json` uses placeholder values (`REPLACE_IN_CD`) for secrets - use environment variables or user secrets locally

### External Dependencies
- **OpenRouterAI** - AI insights (configured via `OpenRouterAI` section)
- **TwelveData** - Live market data (configured via `TwelveData` section)
- **Google Generative AI** - Used in Trades module for AI summaries/coaching
