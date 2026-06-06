# Copilot Instructions

## Build, test, and lint

- Restore packages from the repo root with `dotnet restore`.
- Build the backend through the gateway project with `dotnet build bootstrapper\TradingJournal.ApiGateway\TradingJournal.ApiGateway.csproj`. The gateway references all business modules, so this is the closest thing to a full backend build.
- Run the host with `dotnet run --project bootstrapper\TradingJournal.ApiGateway\TradingJournal.ApiGateway.csproj`.
- Run tests per project with `dotnet test tests\TradingJournal.Tests.Auth\TradingJournal.Tests.Auth.csproj`, `dotnet test tests\TradingJournal.Tests.Trades\TradingJournal.Tests.Trades.csproj`, `dotnet test tests\TradingJournal.Tests.Analytics\TradingJournal.Tests.Analytics.csproj`, `dotnet test tests\TradingJournal.Tests.Psychology\TradingJournal.Tests.Psychology.csproj`, `dotnet test tests\TradingJournal.Tests.Scanner\TradingJournal.Tests.Scanner.csproj`, and `dotnet test tests\TradingJournal.Tests.Integration\TradingJournal.Tests.Integration.csproj`.
- Run a single test with `dotnet test tests\TradingJournal.Tests.Auth\TradingJournal.Tests.Auth.csproj --filter "FullyQualifiedName~TestMethodName"`.
- Format and run analyzers with `dotnet format`.

## High-level architecture

- This backend is a modular monolith with a single ASP.NET Core composition root in `bootstrapper\TradingJournal.ApiGateway\Program.cs`. The host wires Serilog, JWT auth, CORS, rate limiting, idempotency, Swagger/OpenAPI/Scalar, health checks, static files, and two SignalR hubs before mapping Carter endpoints.
- The host registers `TradingJournal.Shared`, all 9 business modules (`Auth`, `Trades`, `Psychology`, `Analytics`, `TradingSetup`, `AiInsights`, `Notifications`, `Scanner`, `RiskManagement`), and the in-memory message queue from `TradingJournal.Messaging.Shared`.
- Most modules own their own EF Core `DbContext`, but they all use the shared `TradeDatabase` connection string. `Analytics` is the notable exception: it is provider-backed and read-only instead of registering its own `DbContext`.
- Cross-module asynchronous work flows through `IEventBus` -> `InMemoryMessageQueue` -> `IntegrationEventProcessorJob` -> MediatR `INotificationHandler<TEvent>`. Notifications and scanner updates are then pushed over SignalR hubs at `/hubs/notifications` and `/hubs/scanner`.
- The repo already has good deeper references. When a task spans multiple modules, start with `docs\README.md`, `docs\TECHNICAL_SPEC.md`, `docs\CODE_FLOW.md`, and `bootstrapper\TradingJournal.ApiGateway\Program.cs`.

## Key conventions

- Follow the vertical-slice layout under `modules\{Module}\TradingJournal.Modules.{Module}\Features\V1\...`. A typical slice keeps `Request`, `Validator`, `Handler`, and `Endpoint` together in one file.
- New reads and writes should use the shared CQRS contracts (`IQuery<T>`, `ICommand<T>`, `ICommandHandler<...>`) and return `Result` / `Result<T>` for business failures instead of inventing new response wrappers.
- If a request needs the authenticated user ID, prefer implementing `IUserAwareRequest` so `UserAwareBehavior` injects `UserId`. Some older slices still set `UserId` manually in the endpoint from `ClaimsPrincipal`, so match the surrounding feature style before refactoring.
- In each module's `DependencyInjection.cs`, use `AddModuleDefaults(...)` and `AddModuleDbContext<TContext>(...)` rather than registering validators, MediatR, and SQL Server options by hand.
- Module `DbContext` implementations should inherit `AuditableDbContext`, and module entities typically inherit `EntityBase<int>`. That brings audit fields, audit-log capture, and the global soft-delete filter on `IsDisabled`.
- For multi-step writes, prefer `ExecuteInTransactionAsync(...)`. `BeginTransaction`, `CommitTransaction`, and `RollbackTransaction` still exist on some module db-context interfaces, but the base implementation marks them obsolete.
- Preserve module boundaries. Prefer provider interfaces and integration events for cross-module collaboration instead of reaching into another module's `DbContext` directly.
- Mutating handlers often need follow-up cache invalidation through `ICacheRepository.RemoveCache(...)`, and some also publish integration events after `SaveChangesAsync(...)`.
- Development startup automatically migrates Trades, Psychology, TradingSetup, AiInsights, Notifications, Scanner, and RiskManagement. Auth is not part of that startup migration block, so fresh environments may still need manual Auth EF migrations.
- SignalR hubs are authenticated with JWT, but hub clients pass the token through the `access_token` query string on `/hubs/*`, and both hubs group connections as `user-{userId}`.
