# Code Flow

## Startup Sequence

```mermaid
flowchart TD
    A[Create builder] --> B[Configure logging, CORS, rate limiting, auth]
    B --> C[Register shared module]
    C --> D[Register business modules]
    D --> E[Register in-memory message queue]
    E --> F[Build app]
    F --> G[Run development migrations]
    G --> H[Wire middleware]
    H --> I[Map endpoints, health checks, docs, hubs]
```

Startup in `Development` currently runs migrations for Trades, Psychology, TradingSetup, AiInsights, Notifications, Scanner, and RiskManagement. Auth is not part of that startup migration block.

## Request Path

```mermaid
flowchart LR
    A[HTTP request] --> B[Carter endpoint]
    B --> C[MediatR request]
    C --> D[ValidationBehavior]
    D --> E[UserAwareBehavior when applicable]
    E --> F[LoggingBehavior]
    F --> G[Handler]
    G --> H[DbContext or provider or service]
    H --> I[Result response]
```

## Async Event Path

```mermaid
flowchart LR
    A[Publisher] --> B[IEventBus]
    B --> C[Channel queue]
    C --> D[IntegrationEventProcessorJob]
    D --> E[MediatR notification handler]
    E --> F[DB write or AI call or SignalR push]
```

## Real-Time Notification Path

1. A feature or event handler calls `INotificationService.CreateAndPushAsync(...)`.
2. Notification data is persisted.
3. The service pushes `NewNotification` to the `user-{userId}` SignalR group.
4. The unread count is recalculated and pushed as `UnreadCountChanged`.

## Hosted Services To Know

- `IntegrationEventProcessorJob`
- `IdempotencyCleanupService`
- `ScannerBackgroundService`
- `EconomicCalendarBackgroundService`

## Related Pages

- [Backend Overview](./Backend-Overview.md)
- [Technical Spec](./Technical-Spec.md)
- [Feature Flow](./Feature-Flow.md)