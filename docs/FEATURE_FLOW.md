# Backend Feature Flow

> Purpose: document end-to-end business journeys across modules.
> Audience: developers aligning frontend work, backend changes, and debugging user-visible behavior.
> Canonical sources: feature slices under `modules/*/Features/V1/*`, event handlers, and core services.

## Coverage

This file focuses on representative backend journeys instead of listing every route.

1. Auth registration, login, and refresh
2. Trade creation
3. Trade close into tilt recalculation and AI intervention
4. Review wizard data and asynchronous AI review generation
5. Scanner alert delivery into notifications
6. Analytics read-model generation
7. Risk management configuration and dashboard reads
8. Trading setup flowchart persistence

## 1. Auth: Register, Login, Refresh

Source anchors:

- [Register.cs](../modules/Auth/TradingJournal.Modules.Auth/Features/V1/Auth/Register.cs)
- [Login.cs](../modules/Auth/TradingJournal.Modules.Auth/Features/V1/Auth/Login.cs)
- [RefreshToken.cs](../modules/Auth/TradingJournal.Modules.Auth/Features/V1/Auth/RefreshToken.cs)

Flow:

1. Registration validates email, password, and full name.
2. The handler checks for duplicate email, hashes the password with BCrypt, inserts `User`, and returns the new user id.
3. Login loads the user, verifies the BCrypt hash, checks `IsActive`, generates a JWT, rotates a refresh token, and persists refresh-token state.
4. Refresh token flow validates the expired access token without enforcing lifetime, re-loads the user, checks the stored refresh token, rotates the token pair, and returns a fresh JWT.

Outcome:

- Auth is stateful for refresh-token rotation and stateless for access-token validation.
- Admin-only routes are protected by policy rather than a separate auth subsystem.

## 2. Trades: Create a Trade

Source anchors:

- [CreateTrade.cs](../modules/Trades/TradingJournal.Modules.Trades/Features/V1/Trade/CreateTrade.cs)
- [DependencyInjection.cs](../modules/Trades/TradingJournal.Modules.Trades/DependencyInjection.cs)

Flow:

1. The trade creation slice validates core trade fields, checklist presence, and zone selection.
2. The handler begins a database transaction.
3. The current user is resolved, checklist ownership is verified, and the request is mapped into `TradeHistory`.
4. Screenshot payloads are validated and persisted through `IScreenshotService`.
5. Related entities such as technical tags, emotion tags, screenshots, and checklist links are inserted.
6. The transaction commits and the per-user trade cache is invalidated.

Outcome:

- A trade is saved together with its related records in one transactional slice.
- Discipline evaluation is part of trade creation, so trade journaling feeds later review and psychology flows.

## 3. Trades + Psychology + AiInsights + Notifications: Close Trade to Intervention

Source anchors:

- [CloseTrade.cs](../modules/Trades/TradingJournal.Modules.Trades/Features/V1/Trade/CloseTrade.cs)
- [TradeClosedTiltRefreshHandler.cs](../modules/Psychology/TradingJournal.Modules.Psychology/EventHandlers/TradeClosedTiltRefreshHandler.cs)
- [TiltDetectionService.cs](../modules/Psychology/TradingJournal.Modules.Psychology/Services/TiltDetectionService.cs)
- [TiltSnapshotUpdatedAiHandler.cs](../modules/AiInsights/TradingJournal.Modules.AiInsights/EventHandlers/TiltSnapshotUpdatedAiHandler.cs)
- [AiTiltInterventionNotificationHandler.cs](../modules/Notifications/TradingJournal.Modules.Notifications/EventHandlers/AiTiltInterventionNotificationHandler.cs)

Flow:

1. Closing a trade updates exit price, PnL, close time, and status.
2. The trade cache is invalidated.
3. Trades publishes `TradeClosedEvent`.
4. Psychology consumes that event and recalculates the user's tilt snapshot.
5. Psychology persists the new tilt snapshot and publishes `TiltSnapshotUpdatedEvent`.
6. AiInsights consumes the tilt update, asks the AI service to classify intervention risk, and only continues for high or critical cases.
7. AiInsights publishes `AiTiltInterventionDetectedEvent`.
8. Notifications consumes the event, persists a notification, and pushes it to the user's SignalR group.

Outcome:

- Trade closure does not directly call the AI or notification modules.
- The user-visible intervention is an event-driven chain that stays modular but remains inside the same process.

## 4. Reviews: Build Review Wizard Data and Generate AI Summary

Source anchors:

- [GetWizardData.cs](../modules/Trades/TradingJournal.Modules.Trades/Features/V1/ReviewWizard/GetWizardData.cs)
- [SaveReviewWizard.cs](../modules/Trades/TradingJournal.Modules.Trades/Features/V1/ReviewWizard/SaveReviewWizard.cs)
- [GenerateReviewSummary.cs](../modules/AiInsights/TradingJournal.Modules.AiInsights/Features/V1/Review/GenerateReviewSummary.cs)
- [GenerateReviewSummaryEventHandler.cs](../modules/AiInsights/TradingJournal.Modules.AiInsights/EventHandlers/GenerateReviewSummaryEventHandler.cs)

Flow:

1. The review wizard read slice builds the current-period snapshot and previous-period snapshot.
2. Trades computes best trades, worst trades, emotion distribution, confidence distribution, discipline summary, pending action items, and review streak.
3. Saving the wizard stores or updates `TradingReview` and its action items inside the Trades module.
4. When the client asks for AI review generation, AiInsights builds the review snapshot through `IAiTradeDataProvider`.
5. AiInsights marks `AiSummaryGenerating = true`, persists that state, and publishes `GenerateReviewSummaryEvent`.
6. The async event handler calls the AI service, writes the generated fields back into `TradingReview`, and clears the generating flag.

Outcome:

- The review wizard UI can read deterministic backend metrics immediately.
- AI review generation is asynchronous and safe to poll.

## 5. Scanner + Notifications: Market Alert Delivery

Source anchors:

- [StartScanner.cs](../modules/Scanner/TradingJournal.Modules.Scanner/Features/V1/Scanner/StartScanner.cs)
- [ScannerEngine.cs](../modules/Scanner/TradingJournal.Modules.Scanner/Services/ScannerEngine.cs)
- [ScannerAlertNotificationHandler.cs](../modules/Notifications/TradingJournal.Modules.Notifications/EventHandlers/ScannerAlertNotificationHandler.cs)

Flow:

1. A user starts the scanner globally or per watchlist.
2. Scanner config is created on demand if it does not already exist.
3. The hosted scanner service loads active watchlists and runs `ScannerEngine`.
4. The engine fetches recent candles, runs detectors, filters by confluence score, deduplicates alerts, and saves new `ScannerAlert` records.
5. Each saved alert is published as `ScannerAlertEvent`.
6. Notifications turns the event into a persisted notification and a real-time SignalR push.

Outcome:

- Scanner work stays isolated in the Scanner module.
- Notification creation remains centralized in the Notifications module.

## 6. Analytics: Read Models over Trade and Setup Providers

Source anchors:

- [GetPerformanceSummary.cs](../modules/Analytics/TradingJournal.Modules.Analytics/Features/V1/GetPerformanceSummary.cs)
- [GetInsights.cs](../modules/Analytics/TradingJournal.Modules.Analytics/Features/V1/GetInsights.cs)
- [GetPlaybookOverview.cs](../modules/Analytics/TradingJournal.Modules.Analytics/Features/V1/GetPlaybookOverview.cs)

Flow:

1. Analytics endpoints accept a filter and the authenticated user context.
2. Analytics loads trade data through `ITradeProvider` and setup data through `ISetupProvider` when needed.
3. Metrics are computed in-memory by the analytics calculators and returned as read models.

Outcome:

- Analytics stays decoupled from write-side persistence.
- Trade and setup modules remain the owners of source-of-truth data.

## 7. Risk Management: Config, Dashboard, and Position Sizing

Source anchors:

- [GetRiskConfig.cs](../modules/RiskManagement/TradingJournal.Modules.RiskManagement/Features/V1/GetRiskConfig.cs)
- [UpsertRiskConfig.cs](../modules/RiskManagement/TradingJournal.Modules.RiskManagement/Features/V1/UpsertRiskConfig.cs)
- [GetRiskDashboard.cs](../modules/RiskManagement/TradingJournal.Modules.RiskManagement/Features/V1/GetRiskDashboard.cs)
- [GetPositionSize.cs](../modules/RiskManagement/TradingJournal.Modules.RiskManagement/Features/V1/GetPositionSize.cs)

Flow:

1. The user reads or upserts risk configuration.
2. Config values are cached for read performance and invalidated after writes.
3. Dashboard reads call into `IRiskContextProvider`, which assembles current state such as daily PnL, weekly drawdown, open positions, and alerts.
4. Position sizing reads combine current config with per-request overrides and calculate units, lots, and stop-loss distance.

Outcome:

- Risk configuration is the persistent base layer.
- Dashboard and position sizing are derived read models over that configuration plus current trading state.

## 8. Trading Setup: Flowchart-Based Playbook Persistence

Source anchors:

- [CreateTradingSetup.cs](../modules/TradingSetup/TradingJournal.Modules.TradingSetup/Features/V1/TradingSetups/CreateTradingSetup.cs)
- [GetTradingSetups.cs](../modules/TradingSetup/TradingJournal.Modules.TradingSetup/Features/V1/TradingSetups/GetTradingSetups.cs)
- [GetTradingSetupDetail.cs](../modules/TradingSetup/TradingJournal.Modules.TradingSetup/Features/V1/TradingSetups/GetTradingSetupDetail.cs)
- [TradingSetupContracts.cs](../modules/TradingSetup/TradingJournal.Modules.TradingSetup/Features/V1/TradingSetups/TradingSetupContracts.cs)

Flow:

1. The client submits nodes and edges for a setup diagram.
2. `TradingSetupDiagram.Validate(...)` enforces node and edge integrity.
3. The handler converts nodes into `SetupStep` records and edges into `SetupConnection` records.
4. The setup is persisted and the per-user setup cache is invalidated.
5. Read slices return summarized setup cards or the full reconstructed graph.

Outcome:

- The playbook is stored as structured graph data rather than opaque JSON.
- Analytics can later consume setup summaries through `ISetupProvider`.