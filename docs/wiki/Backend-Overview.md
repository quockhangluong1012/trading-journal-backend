# Backend Overview

## What This System Is

Trading Journal backend is a modular monolith hosted by a single ASP.NET Core application. The entry point is `bootstrapper/TradingJournal.ApiGateWay/Program.cs`, which composes the shared runtime and all business modules.

## High-Level Shape

- Runtime: .NET 10 + ASP.NET Core minimal APIs
- Slice pattern: Carter endpoints + MediatR request handlers
- Persistence: SQL Server with per-module `DbContext` registrations
- Async integration: in-memory event queue processed by a hosted background service
- Real-time delivery: SignalR hubs for notifications and scanner updates

## Shared Foundation

| Area | Purpose |
|------|---------|
| `TradingJournal.Shared` | Cross-cutting services such as cache, idempotency, audit logging, file storage, and user context |
| `TradingJournal.Messaging.Shared` | `IEventBus`, in-memory queue, and event processor job |
| `ModuleExtensions` | Standard per-module validator, MediatR, and pipeline registration |

## Module Catalog

| Module | Responsibility |
|--------|----------------|
| Auth | Registration, login, refresh token flow, admin/staff APIs |
| Trades | Trade CRUD, dashboards, review data, screenshot and checklist handling |
| Psychology | Daily psychology journals, tilt, streaks, karma, emotion analysis |
| Analytics | Read models and performance calculations over trades and setups |
| TradingSetup | Flowchart-based setup and playbook persistence |
| AiInsights | AI coach, AI review generation, AI validation, AI search, AI digest |
| Notifications | Notification persistence plus SignalR push delivery |
| Scanner | Watchlists, scanner engine, alerts, smart confluence, economic calendar |
| RiskManagement | Risk configuration, dashboard, position sizing, correlation, account balance |

## Where To Start In Code

1. `bootstrapper/TradingJournal.ApiGateWay/Program.cs` for composition and middleware.
2. `shared/TradingJournal.Shared/Extensions/ModuleExtensions.cs` for the default module registration pattern.
3. `modules/*/DependencyInjection.cs` to see what each module owns.
4. `modules/*/Features/V1/*` for request handlers and endpoints.

## Related Pages

- [Technical Spec](./Technical-Spec.md)
- [Code Flow](./Code-Flow.md)
- [Feature Flow](./Feature-Flow.md)