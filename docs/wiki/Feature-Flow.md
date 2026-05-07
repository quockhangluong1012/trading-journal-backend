# Feature Flow

## Main User Journeys

| Journey | Modules involved |
|---------|------------------|
| Register, login, refresh token | Auth |
| Create trade | Trades |
| Close trade to tilt intervention | Trades, Psychology, AiInsights, Notifications |
| Build review wizard and AI review summary | Trades, AiInsights |
| Scanner alert delivery | Scanner, Notifications |
| Risk dashboard and position sizing | RiskManagement, Trades |
| Analytics views | Analytics, Trades, TradingSetup |
| Setup flowchart persistence | TradingSetup |

## Example: Close Trade To Intervention

1. Trades closes the trade and publishes `TradeClosedEvent`.
2. Psychology consumes that event and recalculates tilt.
3. Psychology publishes `TiltSnapshotUpdatedEvent`.
4. AiInsights evaluates whether an intervention is needed.
5. Notifications persists and pushes the intervention if the AI marks it as high or critical.

## Example: Scanner Alert To Notification

1. Scanner engine runs over active watchlists.
2. It detects confluence, deduplicates alerts, and stores `ScannerAlert`.
3. Scanner publishes `ScannerAlertEvent`.
4. Notifications creates a user-facing notification and pushes it over SignalR.

## Example: Review Wizard To AI Summary

1. Trades builds deterministic review metrics for the selected period.
2. The user saves or completes review content.
3. AiInsights marks review generation as in progress.
4. AiInsights publishes `GenerateReviewSummaryEvent`.
5. The async handler calls the AI service and writes the generated summary back to `TradingReview`.

## Related Pages

- [Backend Overview](./Backend-Overview.md)
- [Technical Spec](./Technical-Spec.md)
- [Code Flow](./Code-Flow.md)