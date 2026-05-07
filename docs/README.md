# Backend Documentation Index

> Purpose: fast orientation for engineers working in the backend.
> Audience: developers onboarding, tracing bugs, or planning feature work.
> Canonical sources: `bootstrapper/TradingJournal.ApiGateWay/Program.cs`, `shared/TradingJournal.Shared/Extensions/ModuleExtensions.cs`, `shared/TradingJournal.Messaging.Shared/*`, and each module's `DependencyInjection.cs` plus `Features/V1/*` slices.

## Documentation Map

| Document | Use it for | Does not try to be |
|----------|------------|--------------------|
| [TECHNICAL_SPEC.md](./TECHNICAL_SPEC.md) | Stable architecture, module responsibilities, shared conventions, platform controls | A step-by-step runtime trace |
| [CODE_FLOW.md](./CODE_FLOW.md) | How the code executes at startup, during HTTP requests, during events, and inside hosted services | A feature inventory |
| [FEATURE_FLOW.md](./FEATURE_FLOW.md) | End-to-end business journeys such as login, trade capture, scanner alerts, and AI review generation | A full endpoint reference |

## GitHub Wiki Staging

- [wiki/Home.md](./wiki/Home.md) provides a GitHub Wiki-ready landing page.
- [wiki/_Sidebar.md](./wiki/_Sidebar.md) provides the sidebar navigation structure.
- The `docs/wiki/` folder is a publishing layout for maintainers; the canonical long-form docs remain in this `docs/` folder.

## System Snapshot

- Single ASP.NET Core host under [Program.cs](../bootstrapper/TradingJournal.ApiGateWay/Program.cs)
- .NET 10 + Carter minimal APIs + MediatR vertical slices
- 9 business modules: Auth, Trades, Psychology, Analytics, TradingSetup, AiInsights, Notifications, Scanner, RiskManagement
- SQL Server persistence through per-module `DbContext` registrations; Analytics is the main read-only module without its own database context
- SignalR hubs for notifications and scanner updates
- In-memory event queue plus background dispatcher for cross-module asynchronous work
- Shared cross-cutting services for caching, idempotency, audit logging, file storage, and user context

## Composition Root

The runtime is composed in [Program.cs](../bootstrapper/TradingJournal.ApiGateWay/Program.cs).

1. Configure bootstrap logging, configuration access, CORS, rate limiting, Swagger/OpenAPI/Scalar, JSON options, and JWT auth.
2. Register the shared module and then each business module through its `Add{Module}Module(...)` method.
3. Add the in-memory message queue so event publishers can enqueue integration events.
4. Build the app, run development-only database migrations for Trades, Psychology, TradingSetup, AiInsights, Notifications, Scanner, and RiskManagement, and then wire the HTTP middleware pipeline. Auth is currently not included in that startup migration block.
5. Map Carter endpoints, health checks, API docs, audit log endpoint, and SignalR hubs.

## Shared Foundation

### TradingJournal.Shared

- [DependencyInjection.cs](../shared/TradingJournal.Shared/DependencyInjection.cs) registers `IDateTimeProvider`, `IUserContext`, `IFileStorageService`, `ICacheRepository`, idempotency infrastructure, and audit logging.
- [ModuleExtensions.cs](../shared/TradingJournal.Shared/Extensions/ModuleExtensions.cs) is the standard module bootstrapper for validators, MediatR handlers, and pipeline behaviors.
- `AuditableDbContext` and `EntityBase<T>` provide audit stamps and soft-delete support.

### TradingJournal.Messaging.Shared

- [DependencyInjection.cs](../shared/TradingJournal.Messaging.Shared/DependencyInjection.cs) registers the channel-backed queue, `IEventBus`, and the background event processor.
- The event bus is process-local and eventually consistent inside the monolith.

## Module Map

| Module | Primary responsibility | Best starting anchors |
|--------|------------------------|-----------------------|
| Auth | Registration, login, refresh tokens, admin/staff APIs | [DependencyInjection.cs](../modules/Auth/TradingJournal.Modules.Auth/DependencyInjection.cs), `Features/V1/Auth/*` |
| Trades | Trade CRUD, trade review data, dashboard metrics, screenshots, discipline context | [DependencyInjection.cs](../modules/Trades/TradingJournal.Modules.Trades/DependencyInjection.cs), `Features/V1/Trade/*`, `Features/V1/ReviewWizard/*` |
| Psychology | Daily psychology journals, emotions, tilt, streaks, karma | [DependencyInjection.cs](../modules/Psychology/TradingJournal.Modules.Psychology/DependencyInjection.cs), `Features/V1/Psychology/*`, `Services/TiltDetectionService.cs` |
| Analytics | Read-model analytics over trades and setups | [DependencyInjection.cs](../modules/Analytics/TradingJournal.Modules.Analytics/DependencyInjection.cs), `Features/V1/*` |
| TradingSetup | Playbook/setup flowcharts and setup metadata | [DependencyInjection.cs](../modules/TradingSetup/TradingJournal.Modules.TradingSetup/DependencyInjection.cs), `Features/V1/TradingSetups/*` |
| AiInsights | AI coach, AI review summaries, AI search, AI validation, AI digest | [DependencyInjection.cs](../modules/AiInsights/TradingJournal.Modules.AiInsights/DependencyInjection.cs), `Features/V1/*`, `EventHandlers/*` |
| Notifications | Notification persistence and SignalR push delivery | [DependencyInjection.cs](../modules/Notifications/TradingJournal.Modules.Notifications/DependencyInjection.cs), `Features/V1/*`, `Services/NotificationService.cs` |
| Scanner | Watchlists, scanner engine, economic calendar, scanner alerts, smart confluence | [DependencyInjection.cs](../modules/Scanner/TradingJournal.Modules.Scanner/DependencyInjection.cs), `Features/V1/*`, `Services/ScannerEngine.cs` |
| RiskManagement | Risk config, dashboard, position sizing, correlation, account-balance entries | [DependencyInjection.cs](../modules/RiskManagement/TradingJournal.Modules.RiskManagement/DependencyInjection.cs), `Features/V1/*` |

## Common Reading Paths

### Follow an HTTP endpoint

1. Start in [Program.cs](../bootstrapper/TradingJournal.ApiGateWay/Program.cs) to confirm the host and middleware.
2. Open the feature slice under `modules/*/Features/V1/*`.
3. Read `Endpoint`, then `Request`, then `Validator`, then `Handler` in the same file.
4. If the handler delegates, step to the injected service or provider.

### Follow an asynchronous event

1. Find the `eventBus.PublishAsync(...)` call in a handler or service.
2. Confirm the queue path in [EventBus.cs](../shared/TradingJournal.Messaging.Shared/Events/EventBus.cs) and [IntegrationEventProcessorJob.cs](../shared/TradingJournal.Messaging.Shared/Events/IntegrationEventProcessorJob.cs).
3. Find the consuming `INotificationHandler<TEvent>` implementation in the target module.

### Follow a real-time notification

1. Start at the event handler or REST slice that calls `INotificationService`.
2. Read [NotificationService.cs](../modules/Notifications/TradingJournal.Modules.Notifications/Services/NotificationService.cs).
3. Confirm SignalR group semantics in [NotificationHub.cs](../modules/Notifications/TradingJournal.Modules.Notifications/Hubs/NotificationHub.cs).

## Documentation Contract

- This index is the entry point and orientation layer for the repo docs.
- [TECHNICAL_SPEC.md](./TECHNICAL_SPEC.md) owns stable architecture facts and module boundaries.
- [CODE_FLOW.md](./CODE_FLOW.md) owns runtime mechanics.
- [FEATURE_FLOW.md](./FEATURE_FLOW.md) owns end-to-end business journeys.
- If documentation drifts, trust the code anchors listed at the top of this file before updating prose.