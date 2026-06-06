# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Common Commands

### Build
```bash
dotnet build bootstrapper/TradingJournal.ApiGateway/TradingJournal.ApiGateway.csproj
```
The API Gateway references all modules, so building it compiles the entire application. The whole solution can also be built via `dotnet build TradingJournal.slnx`.

### Run
```bash
dotnet run --project bootstrapper/TradingJournal.ApiGateway/TradingJournal.ApiGateway.csproj
```
In Development, the app auto-applies EF migrations on startup for most modules (Trades, Psychology, Backtest, TradingSetup, AiInsights, Notifications, Scanner, RiskManagement). The Auth module is **not** auto-migrated — apply its migrations manually.

### Test
Run a module's tests:
```bash
dotnet test tests/TradingJournal.Tests.Auth/TradingJournal.Tests.Auth.csproj
dotnet test tests/TradingJournal.Tests.Trades/TradingJournal.Tests.Trades.csproj
# Other test projects: Analytics, Psychology, Scanner, Backtest, Integration
```

Run a single test:
```bash
dotnet test tests/TradingJournal.Tests.Auth/TradingJournal.Tests.Auth.csproj --filter "FullyQualifiedName~TestMethodName"
```

Integration tests spin up a **real SQL Server via Testcontainers.MsSql** (Docker required), not an in-memory provider. The shared `TradingJournalWebFactory` (`WebApplicationFactory<Program>`) replaces connection strings and AI services, and `CreateAuthenticatedClient(userId, role)` issues a test JWT via `TestJwtGenerator`. AI calls are stubbed with `FakeOpenRouterAiService`. Unit-level mocking uses Moq.

### EF Core Migrations
Add migration for a module (e.g., Auth):
```bash
dotnet ef migrations add <MigrationName> \
  --project modules/Auth/TradingJournal.Modules.Auth/TradingJournal.Modules.Auth.csproj \
  --startup-project bootstrapper/TradingJournal.ApiGateway/TradingJournal.ApiGateway.csproj
```

Apply migrations:
```bash
dotnet ef database update \
  --project modules/Auth/TradingJournal.Modules.Auth/TradingJournal.Modules.Auth.csproj \
  --startup-project bootstrapper/TradingJournal.ApiGateway/TradingJournal.ApiGateway.csproj
```

### Format/Lint
```bash
dotnet format
```
Code analyzers (Microsoft.CodeAnalysis.Analyzers) are enabled project-wide.

## Architecture

### Modular Monolith
A modular monolith with a single entry point (`TradingJournal.ApiGateway`) that references all business modules. Each module is a class library with its own DbContext, EF Core migrations, and vertical-slice features. Modules do not reference each other — cross-module communication is via integration events (see below).

### Project Structure
- **bootstrapper/TradingJournal.ApiGateway** — ASP.NET Core entry point. References all modules; configures auth (JWT + Google), CORS, rate limiting, Swagger/Scalar docs (Development only), Serilog structured logging with per-request correlation IDs, SignalR, and the in-memory message queue. `Program.cs` wires modules via `AddXModule(...)` extension calls.
- **modules/** — Business modules, each with its own DbContext:
  - `Auth` — Authentication, BCrypt hashing, JWT issuance
  - `Trades` — Trade history, technical analysis, AI summaries, review wizard, trade templates
  - `AiInsights` — AI-powered insights
  - `Psychology` — Trading psychology tracking
  - `TradingSetup` — Trading setup/playbook management
  - `Notifications` — Notification system (SignalR)
  - `Scanner` — Market scanner with watchlist, ICT detectors, economic calendar, live data
  - `RiskManagement` — Risk rules and guardrails
  - `Backtest` — Backtesting sessions, order-matching engine, market data, CSV import (uses a **separate** database)
  - `Analytics` — Trading analytics; **read-only and provider-backed, no DbContext registered**
- **shared/**:
  - `TradingJournal.Shared` — Domain abstractions and module DI helpers (see Shared Abstractions)
  - `TradingJournal.Messaging.Shared` — Integration-event contracts and the event bus
- **tests/** — xUnit test projects mirroring each module, plus an Integration project.

Solution file: `TradingJournal.slnx` (VS2022 slnx format).

### Vertical Slice Feature Pattern
This is the dominant pattern — internalize it before adding features. Each operation lives in **one file** under `Features/V1/{Feature}/{Operation}.cs`, containing the request, validator, handler, and Carter endpoint together:

```csharp
public sealed class CreateTrade
{
    public record Request(...) : ICommand<Result<int>>;          // or IQuery<Result<T>>

    public sealed class Validator : AbstractValidator<Request> { ... }  // FluentValidation

    public sealed class Handler(ITradeDbContext context, ...)
        : ICommandHandler<Request, Result<int>>                  // or IQueryHandler<,>
    {
        public async Task<Result<int>> Handle(Request request, CancellationToken ct)
            => await context.ExecuteInTransactionAsync(async innerCt => { ... });
    }

    public class Endpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app) =>
            app.MapGroup(ApiGroup.V1.TradeHistory)
               .MapPost("/", (Request req, ISender sender) => sender.Send(req))
               .RequireAuthorization();
    }
}
```

Typical module folders: `Features/V1/{Feature}/`, `Domain/`, `Dto/`, `Infrastructure/` (DbContext + EF configs), `Services/`, `Common/` (enums/constants), `Events/`, `Hubs/` (SignalR), `Migrations/`, and `DependencyInjection.cs`.

### Module Registration
Each module exposes a static `AddXModule(this IServiceCollection, IConfiguration, bool isDevelopment)` extension (in its `DependencyInjection.cs`), called from `Program.cs`. Inside, two shared helpers from `TradingJournal.Shared` do most of the wiring:
- `AddModuleDefaults(assembly, isDevelopment)` — auto-registers FluentValidation validators and MediatR handlers from the assembly, plus the open pipeline behaviors `ValidationBehavior<,>`, `UserAwareBehavior<,>`, `LoggingBehavior<,>`.
- `AddModuleDbContext<TContext>(connectionString)` — registers the DbContext with SQL Server and retry-on-failure.

Carter endpoints need no manual registration — `app.MapCarter()` auto-discovers all `ICarterModule` implementations across loaded assemblies.

### Cross-Module Communication (Integration Events)
Modules never call each other directly. They publish `IntegrationEvent` records (contracts in `TradingJournal.Messaging.Shared/Contracts`, e.g. `TradeClosedEvent`) via `IEventBus`. The `InMemoryMessageQueue` + `IntegrationEventProcessorJob` dispatch them to MediatR `INotificationHandler<TEvent>` handlers in subscribing modules. Register with `AddInMemoryMessageQueue()`.

### SignalR
Three hubs push real-time updates: `/hubs/notifications`, `/hubs/scanner`, `/hubs/backtest`. Browser clients authenticate by passing the JWT via the `access_token` query-string parameter (standard SignalR pattern).

### Data Access
- EF Core 10 with SQL Server; each module owns an isolated DbContext.
- Most modules share the `TradeDatabase` connection string; **Backtest uses `BacktestDatabase`**.
- DbContexts derive from `AuditableDbContext` (in `TradingJournal.Shared`), which auto-populates `CreatedDate/CreatedBy/UpdatedDate/UpdatedBy`, captures an audit trail, and exposes `ExecuteInTransactionAsync(...)`. Entities derive from `EntityBase<T>` (Id, audit fields, `IsDisabled` soft-delete).
- Financial columns use `decimal(18,5)` / `decimal(18,2)`; precision is asserted in tests (`TradeDbContextPrecisionTests`).

### Shared Abstractions (`TradingJournal.Shared`)
- `Result` / `Result<T>` + `Error` — success/failure return type used by all handlers (`.IsSuccess`, `.Value`, `.Errors`).
- `ICommand<T>` / `IQuery<T>` and `ICommandHandler<,>` / `IQueryHandler<,>` — MediatR markers used instead of raw `IRequest`.
- `IUserAwareRequest` — marker that makes `UserAwareBehavior` inject the current `UserId` into the request.
- `ICacheRepository` (over HybridCache), `IIdempotencyStore`/`SqlIdempotencyStore`, `IAuditLogStore`/`SqlAuditLogStore`.

### Key Patterns Summary
- **Routing**: Carter minimal APIs (no controllers)
- **CQRS**: MediatR with the `ICommand`/`IQuery` markers above
- **Validation**: FluentValidation via `ValidationBehavior`
- **Mapping**: Mapster
- **Auth**: JWT Bearer + Google OAuth, configured in the Gateway
- **Configuration**: `appsettings.json` uses `REPLACE_IN_CD` placeholders for secrets — use environment variables or user secrets locally

### External Dependencies
- **OpenRouterAI** — AI insights (`OpenRouterAI` config section)
- **TwelveData** — Live market data (`TwelveData` config section)
- **Google Generative AI** — AI summaries/coaching in the Trades module
