# Trading Journal Backend — Technical Specification

> **Last updated:** 2026-05-07
> **Runtime:** .NET 10 | **Database:** SQL Server | **Architecture:** Modular monolith

---

## 1. System Overview

**Trading Journal** is a single ASP.NET Core host that composes **9 business modules** using **Vertical Slice Architecture** with **CQRS**. The runtime uses **Carter** for minimal API routing, **MediatR** for slice dispatch, **Entity Framework Core 10** for persistence, **SignalR** for real-time delivery, and a **channel-backed in-memory event bus** for asynchronous cross-module work.

### Companion Documents

- [Backend Docs Index](./README.md) — orientation and module map
- [Code Flow](./CODE_FLOW.md) — startup, request, event, and background execution flow
- [Feature Flow](./FEATURE_FLOW.md) — end-to-end business journeys

### Technology Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| Runtime | .NET 10 | Single host application |
| Web Framework | ASP.NET Core Minimal APIs | Hosted in API gateway |
| Routing | Carter | Feature slices implement `ICarterModule` |
| CQRS / Mediator | MediatR | Commands, queries, notification handlers |
| Validation | FluentValidation | Registered per module |
| ORM | Entity Framework Core 10 | SQL Server provider |
| Database | SQL Server | Shared connection string, per-module `DbContext` |
| Real-time | ASP.NET Core SignalR | Notifications and scanner hubs |
| Auth | JWT Bearer | SignalR tokens accepted via `access_token` query string |
| Caching | HybridCache | Exposed through `ICacheRepository` |
| Logging | Serilog | Bootstrap logger + HTTP logging |
| API Docs | Swagger + OpenAPI + Scalar | Docs UI and schema endpoints |
| AI Integration | OpenRouter AI | Used by AiInsights |
| Market Data | Yahoo Finance + Forex Factory scraping | Scanner and economic calendar |

---

## 2. Architecture

### 2.1 High-Level Architecture

```mermaid
graph TB
    subgraph "API Gateway (Bootstrapper)"
        GW["TradingJournal.ApiGateWay<br/>Program.cs — Single entry point"]
    end

    subgraph "Shared Libraries"
        SH["TradingJournal.Shared<br/>EntityBase, Result, CQRS, Behaviors"]
        MS["TradingJournal.Messaging.Shared<br/>IEventBus, IntegrationEvent"]
    end

    subgraph "Feature Modules"
        AUTH["Auth Module"]
        TRADE["Trades Module"]
        PSYCH["Psychology Module"]
        ANALYTICS["Analytics Module"]
        SETUP["TradingSetup Module"]
        AI["AiInsights Module"]
        NOTIF["Notifications Module"]
        SCAN["Scanner Module"]
        RISK["RiskManagement Module"]
    end

    GW --> AUTH & TRADE & PSYCH & ANALYTICS & SETUP & AI & NOTIF & SCAN & RISK
    AUTH & TRADE & PSYCH & ANALYTICS & SETUP & AI & NOTIF & SCAN & RISK --> SH
    SCAN & AI --> MS
    MS --> NOTIF
```

### 2.2 Architectural Patterns

| Pattern | Implementation |
|---------|---------------|
| **Modular Monolith** | 9 modules under `/modules/`, with module-owned dependencies and boundaries |
| **Vertical Slice** | Each feature is a single file containing Request, Validator, Handler, and Endpoint |
| **CQRS** | Commands (`ICommand<T>`) and Queries (`IQuery<T>`) via MediatR |
| **Result Pattern** | `Result<T>` / `Result` for error handling without exceptions |
| **Event-Driven** | In-memory `IEventBus` with `IntegrationEvent` records for cross-module communication |
| **Soft Delete** | Global query filter via `AuditableDbContext` (`IsDisabled` flag) |
| **Audit Trail** | `EntityBase<T>` provides `CreatedDate`, `CreatedBy`, `UpdatedDate`, `UpdatedBy` |
| **User-Aware Requests** | `IUserAwareRequest` + `UserAwareBehavior` auto-injects `UserId` from JWT claims |

### 2.3 Solution Structure

```
trading-journal-backend/
├── TradingJournal.slnx
├── bootstrapper/
│   └── TradingJournal.ApiGateWay/         # Single host / composition root
├── shared/
│   ├── TradingJournal.Shared/             # Core abstractions and cross-cutting services
│   └── TradingJournal.Messaging.Shared/   # Event bus infrastructure
├── modules/
│   ├── Auth/
│   ├── Trades/
│   ├── Psychology/
│   ├── Analytics/
│   ├── TradingSetup/
│   ├── AiInsights/
│   ├── Notifications/
│   ├── Scanner/
│   └── RiskManagement/
└── tests/                                 # Per-module test projects
```

### 2.4 Module Internal Structure (Canonical)

```
TradingJournal.Modules.{Name}/
├── Common/Constants/ & Enums/
├── Domain/              # EF Core entities (EntityBase<int>)
├── Dto/                 # Data transfer objects
├── Features/V1/         # Versioned vertical slices
├── Hubs/                # SignalR hubs (optional)
├── Infrastructure/      # IDbContext + implementation
├── Services/            # Domain/background services
├── Events/              # Published integration events
├── EventHandlers/       # Consumed integration events
├── Migrations/          # EF Core migrations
├── DependencyInjection.cs
└── GlobalUsings.cs
```

---

## 3. Shared Infrastructure

### 3.1 EntityBase

```csharp
public abstract class EntityBase<T>
{
    [Key, DatabaseGenerated(Identity)]
    public required T Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public int CreatedBy { get; set; }
    public bool IsDisabled { get; set; } = false;   // Soft delete
    public DateTime? UpdatedDate { get; set; }
    public int? UpdatedBy { get; set; }
}
```

### 3.2 Result Pattern

```csharp
Result.Success() / Result.Failure(error)
Result<T>.Success(value) / Result<T>.Failure(error)
```

### 3.3 CQRS Interfaces

- `ICommand<TResponse>` — write operations
- `IQuery<TResponse>` — read operations
- `IUserAwareRequest` — auto-injects `UserId` from JWT via `UserAwareBehavior`
- `ICachedQuery<T>` — query caching via HybridCache

### 3.4 MediatR Pipeline Behaviors

1. **ValidationBehavior** — FluentValidation before handler
2. **UserAwareBehavior** — Injects authenticated user ID for `IUserAwareRequest`
3. **LoggingBehavior** — Request/response logging in all environments; log level is configuration-driven

### 3.5 AuditableDbContext

Base class for all module DbContexts:
- Auto-populates `CreatedDate`/`CreatedBy` on insert, `UpdatedDate`/`UpdatedBy` on update
- Global query filter: `HasQueryFilter(e => !e.IsDisabled)`
- Transaction management

### 3.6 Event Bus (In-Memory)

- `IEventBus.PublishAsync<T>(event)` → `InMemoryMessageQueue`
- `IntegrationEventProcessorJob` (hosted service) dequeues and dispatches
- Events are `record` types extending `IntegrationEvent(Guid EventId)`

---

## 4. Database Architecture

### 4.1 Database Topology

| Database | Modules |
|----------|---------|
| `TradeDatabase` connection string | Auth, Trades, Psychology, TradingSetup, AiInsights, Notifications, Scanner, RiskManagement |

`Analytics` reads through provider interfaces and does not register its own `DbContext`.

### 4.2 Schema Isolation

| Module | Schema |
|--------|--------|
| Trades | `Trades` |
| Notifications | `Notification` |
| Scanner | `Scanner` |
| Others | Default |

---

## 5. Authentication & Authorization

- **JWT Bearer** (configuration-driven issuer/audience/secret, zero clock skew)
- **SignalR auth**: JWT via `access_token` query param for `/hubs/*`
- **Admin policy**: `RequireRole("Admin")`
- **Rate limiting**: Global 120/min per IP; Auth 5 requests / 15 minutes per IP+method by default

> Note: the gateway project references the Google authentication package, but the current composition root in `Program.cs` only wires JWT bearer authentication.

---

## 6. API Gateway

`Program.cs` registers the shared module, all 9 business modules, the in-memory message queue, and the runtime pipeline. It also configures JWT auth, CORS, rate limiting, health checks, OpenAPI/Scalar, and 2 SignalR hubs.

| Hub | Path | Purpose |
|-----|------|---------|
| `NotificationHub` | `/hubs/notifications` | Push notifications |
| `ScannerHub` | `/hubs/scanner` | Scanner alerts & status |

---

## 7. Module Catalog

### 7.1 Auth Module

User registration, JWT auth, Google OAuth, admin management.

**Feature Groups:** Auth (Login/Register/OAuth/Refresh), Staffs, AdminDashboard

---

### 7.2 Trades Module (`Trades` schema)

Core trade journaling — CRUD for trade history with full risk management tracking.

**Domain Entities (11):** `TradeHistory`, `TradeScreenShot`, `TradeEmotionTag`, `TradeHistoryChecklist`, `TradeTechnicalAnalysisTag`, `PretradeChecklist`, `ChecklistModel`, `TechnicalAnalysis`, `TradingSession`, `TradingZone`, `TradingProfile`

**Key fields on TradeHistory:** Asset, Position, Entry/Exit prices, PnL, SL/TP tiers, ConfidenceLevel, IsRuleBroken

**Feature Groups (11):** Trade, Screenshots, Checklists, ChecklistModels, TechnicalAnalysis, TradingSession, TradingZone, TradingProfile, Dashboard, Review, AiCoach

**Cross-module:** Implements `ITradeProvider`, `IAiTradeDataProvider`

---

### 7.3 Psychology Module

Trader psychology journaling — emotion tracking, confidence assessment.

**Domain Entities:** `PsychologyJournal`, `PsychologyJournalEmotion`, `EmotionTag`, `ConfidentLevel`

**Cross-module:** Implements `IPsychologyProvider`, `IEmotionTagProvider`

---

### 7.4 Analytics Module (Read-Only)

No own DB — computes analytics from provider-backed read models.

**Representative slices:** `GetPerformanceSummary`, `GetEquityCurve`, `GetInsights`, `GetPlaybookOverview`

---

---

### 7.5 TradingSetup Module

Reusable trading setup templates with step-by-step entry criteria.

**Domain Entities:** `TradingSetup`, `SetupStep`, `SetupConnection`

---

### 7.6 AiInsights Module

AI-powered review, coaching, validation, and search workflows using OpenRouter.

**Domain Entities:** `TradingReview`

**Representative slices:** `ChatWithCoach`, `GenerateReviewSummary`, `SearchTradesNaturalLanguage`, `AnalyzeChartScreenshot`, `GenerateRiskAdvice`, `GenerateWeeklyDigestNotification`

**Event Handling:**
- Consumes `TiltSnapshotUpdatedEvent`
- Processes `GenerateReviewSummaryEvent` asynchronously inside the module
- Publishes `AiTiltInterventionDetectedEvent` and `AiWeeklyDigestGeneratedEvent`

---

### 7.7 Notifications Module (`Notification` schema)

Cross-module notification system with real-time SignalR push.

**Domain Entity — Notification:**
- `UserId`, `Title` (200), `Message` (1000)
- `Type`: System, ScannerAlert, TradeReminder, AiInsight
- `Priority`: Low, Normal, High, Critical
- `IsRead`, `ReadAt`, `Metadata` (JSON, 4000), `ActionUrl` (500)

**REST Endpoints:**

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/notifications` | Paginated list |
| PUT | `/api/v1/notifications/{id}/read` | Mark as read |
| PUT | `/api/v1/notifications/read-all` | Mark all read |
| DELETE | `/api/v1/notifications/{id}` | Soft-delete |
| GET | `/api/v1/notifications/unread-count` | Unread count |

**SignalR:** `NotificationHub` — user group `user-{userId}`, pushes `NewNotification`, `NotificationRead`, `UnreadCountChanged`

---

### 7.8 Scanner Module (`Scanner` schema) — Most Complex

Real-time algorithmic scanner detecting ICT patterns across multiple timeframes.

#### Domain Entities

| Entity | Key Fields |
|--------|-----------|
| `Watchlist` | Name, UserId, IsActive, `IsScannerRunning` (persisted) |
| `WatchlistAsset` | WatchlistId, Symbol, DisplayName, EnabledDetectors[] |
| `WatchlistAssetDetector` | Per-asset pattern override (PatternType, IsEnabled) |
| `ScannerConfig` | Per-user: ScanIntervalSeconds (300), MinConfluenceScore, EnabledPatterns[], EnabledTimeframes[] |
| `ScannerAlert` | Symbol, PatternType, Timeframe, PriceAtDetection, ZoneHigh/Low, ConfluenceScore, DetectedAt, IsDismissed |

#### ICT Pattern Detectors (17)

| # | Detector | # | Detector |
|---|----------|---|----------|
| 1 | FVG | 10 | Market Structure Shift |
| 2 | Order Block | 11 | Change of Character |
| 3 | Breaker Block | 12 | Displacement |
| 4 | Liquidity Pool | 13 | Optimal Trade Entry |
| 5 | Liquidity Sweep | 14 | Judas Swing |
| 6 | Inversion FVG | 15 | Balanced Price Range |
| 7 | Unicorn Model | 16 | CISD |
| 8 | Venom Model | 17 | SMT Divergence (multi-asset) |
| 9 | Mitigation Block | | |

All implement `IIctDetector` (single-asset) or `IMultiAssetDetector` (SMT Divergence).

#### Scanner Engine Pipeline

```mermaid
flowchart TD
    A["ScannerBackgroundService (30s cycle)"] --> B["Query active watchlists"]
    B --> C["ScannerEngine.ScanForWatchlistAsync()"]
    C --> D["Load config + build per-asset pattern map"]
    D --> E["Fetch LIVE candles via YahooFinance"]
    E --> F["MultiTimeframeAnalyzer: run detectors × timeframes"]
    F --> G["Calculate confluence score"]
    G --> H["Filter by MinConfluenceScore"]
    H --> I["Dedup check (4h window)"]
    I --> J["Persist ScannerAlert"]
    J --> K["Publish ScannerAlertEvent → EventBus"]
    K --> L["Notifications module creates notification + SignalR push"]
```

#### Scanner REST Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/scanner/watchlists` | Create watchlist |
| GET | `/api/v1/scanner/watchlists` | List watchlists |
| PUT | `/api/v1/scanner/watchlists/{id}` | Update watchlist |
| DELETE | `/api/v1/scanner/watchlists/{id}` | Delete watchlist |
| POST | `/api/v1/scanner/watchlists/{id}/assets` | Add asset |
| DELETE | `/api/v1/scanner/watchlists/{id}/assets/{assetId}` | Remove asset |
| PUT | `/api/v1/scanner/watchlists/{id}/start` | Start scanner |
| PUT | `/api/v1/scanner/watchlists/{id}/stop` | Stop scanner |
| GET | `.../assets/{assetId}/detectors` | Get per-asset detectors |
| PUT | `.../assets/{assetId}/detectors` | Update per-asset detectors |
| POST | `/api/v1/scanner/start` | Start global scanner |
| POST | `/api/v1/scanner/stop` | Stop global scanner |
| GET | `/api/v1/scanner/status` | Get scanner status |
| GET | `/api/v1/scanner/alerts` | List alerts |
| PUT | `/api/v1/scanner/alerts/{id}/dismiss` | Dismiss alert |
| GET | `/api/v1/scanner/economic-calendar` | Full calendar |
| GET | `.../economic-calendar/upcoming-high-impact` | High-impact events |

#### Economic Calendar

- `EconomicCalendarProvider` — scrapes Forex Factory
- `EconomicCalendarBackgroundService` — periodic refresh
- No API key required

---

### 7.9 RiskManagement Module

Risk configuration and read models for position sizing, exposure, drawdown, and account balance tracking.

**Representative slices:** `GetRiskConfig`, `UpsertRiskConfig`, `GetRiskDashboard`, `GetPositionSize`, `GetCorrelationMatrix`, `CreateAccountBalanceEntry`

**Cross-module:** Consumes trade read models via shared providers to build derived risk views.

---

## 8. Cross-Module Communication

```mermaid
flowchart LR
    SCAN["Scanner"] -->|ScannerAlertEvent| EB["IEventBus"]
    TRADE["Trades"] -->|TradeClosedEvent| EB
    PSYCH["Psychology"] -->|TiltSnapshotUpdatedEvent| EB
    AI["AiInsights"] -->|AiTiltInterventionDetectedEvent / AiWeeklyDigestGeneratedEvent| EB
    EB --> NOTIF["Notifications"]
    EB --> PSYCH
    EB --> AI
```

**Shared Interfaces:**

| Interface | Provider | Consumer |
|-----------|----------|----------|
| `ITradeProvider` | Trades | Analytics, Psychology, AiInsights, RiskManagement, Scanner |
| `IPsychologyProvider` | Psychology | Trades, AiInsights |
| `IEmotionTagProvider` | Psychology | Trades, Psychology |
| `IAiTradeDataProvider` | Trades | AiInsights |
| `ISetupProvider` | TradingSetup | Analytics, AiInsights |
| `ICacheRepository` | Shared | All modules |
| `IDateTimeProvider` | Shared | All modules |
| `IUserContext` | Shared | All modules |

**Key Event Contracts:**

| Event | Publisher | Consumer |
|-------|-----------|----------|
| `TradeClosedEvent` | Trades | Psychology |
| `TiltSnapshotUpdatedEvent` | Psychology | AiInsights |
| `AiTiltInterventionDetectedEvent` | AiInsights | Notifications |
| `AiWeeklyDigestGeneratedEvent` | AiInsights | Notifications |
| `ScannerAlertEvent` | Scanner | Notifications |
| `GenerateReviewSummaryEvent` | AiInsights feature slice | AiInsights async worker |

---

## 9. Security

| Concern | Implementation |
|---------|---------------|
| Authentication | JWT Bearer with issuer/audience/signing-key validation |
| Authorization | `[Authorize]` + `AdminOnly` policy |
| CORS | Explicit origin whitelist from configuration |
| Rate Limiting | Fixed-window per IP |
| HSTS | Enabled outside development, 365 days, preload |
| Security Headers | CSP, frame protection, referrer policy, permissions policy |
| Idempotency | Opt-in `Idempotency-Key` support for mutating requests |
| Soft Delete | Global query filter |
| Audit Trail | Auto `CreatedBy`/`UpdatedBy` from JWT |
| Input Validation | FluentValidation pipeline behavior |

---

## 10. Observability

- Serilog bootstrap logger plus configured sinks from `appsettings*.json`
- Structured logging via `ILogger<T>` and `UseSerilogHttpLogging()`
- `LoggingBehavior` MediatR pipeline on slice execution
- SQL health check mapped at `/health`
- API docs surfaced through Swagger, OpenAPI, and Scalar

---

## 11. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Modular monolith | Single-dev; module boundaries enable future microservice extraction |

| In-memory event bus | Sufficient for single-process; upgradable to RabbitMQ/Kafka |
| Per-watchlist scanner control | Granular; `IsScannerRunning` persisted in DB survives restarts |
| 4-hour dedup window | Prevents alert fatigue for recurring patterns |
| Yahoo Finance primary | Free, no API key, supports all symbols |
| Singleton detectors | ICT detectors are stateless pure functions |
| Carter over controllers | Minimal API routing with module endpoint grouping |
| Vertical slice architecture | Each feature self-contained in single file; easy to navigate |
