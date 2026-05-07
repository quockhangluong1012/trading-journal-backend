# Technical Spec

## Purpose

This page is the concise architecture page for a GitHub Wiki sidebar.

## Platform Summary

| Concern | Current implementation |
|---------|------------------------|
| Host | Single ASP.NET Core gateway |
| Architecture | Modular monolith + vertical slices |
| Dispatch | MediatR commands, queries, and notification handlers |
| Routing | Carter `ICarterModule` endpoints |
| Persistence | SQL Server via EF Core 10 |
| Cache | `HybridCache` through `ICacheRepository` |
| Auth | JWT bearer authentication |
| Real-time | SignalR notification and scanner hubs |
| Async work | In-memory event bus + hosted event processor |
| Logging | Serilog + request logging |

## Cross-Cutting Rules

- Validation runs through `ValidationBehavior`.
- `UserAwareBehavior` injects `UserId` for requests that implement `IUserAwareRequest`.
- `LoggingBehavior` traces MediatR execution.
- Idempotency is opt-in via the `Idempotency-Key` header for mutating requests.
- Security headers, CORS, rate limiting, authentication, and authorization are applied at the gateway.

## Architecture Boundaries

- Shared infrastructure belongs under `shared/`.
- Business behavior belongs inside modules.
- Each feature slice should remain readable from `Endpoint` to `Handler` inside one file when practical.
- Cross-module coordination should prefer shared interfaces or integration events instead of direct database coupling.

## Related Pages

- [Backend Overview](./Backend-Overview.md)
- [Code Flow](./Code-Flow.md)
- [Feature Flow](./Feature-Flow.md)