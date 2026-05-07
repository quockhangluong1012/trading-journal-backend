# Trading Journal Backend Wiki

This folder is a wiki-ready staging layout for the backend documentation. If you publish to a GitHub Wiki, copy these files into the wiki root so `Home.md`, `_Sidebar.md`, and `_Footer.md` are picked up automatically.

## Start Here

- [Backend Overview](./Backend-Overview.md)
- [Technical Spec](./Technical-Spec.md)
- [Code Flow](./Code-Flow.md)
- [Feature Flow](./Feature-Flow.md)
- [Publishing Notes](./Publishing-Notes.md)

## Recommended Reading Paths

| Goal | Read in this order |
|------|--------------------|
| Onboard to the backend | `Backend Overview` -> `Technical Spec` -> `Code Flow` |
| Trace a request bug | `Code Flow` -> `Feature Flow` -> module slice in code |
| Understand a user journey | `Feature Flow` -> `Code Flow` -> feature slice in code |
| Review architecture boundaries | `Technical Spec` -> `Backend Overview` |

## Quick System Summary

- Single ASP.NET Core host under `bootstrapper/TradingJournal.ApiGateWay/Program.cs`
- 9 business modules: Auth, Trades, Psychology, Analytics, TradingSetup, AiInsights, Notifications, Scanner, RiskManagement
- Shared infrastructure for cache, idempotency, audit logging, file storage, user context, and event dispatch
- SignalR hubs for notifications and scanner delivery
- SQL Server persistence with per-module `DbContext` registrations, except Analytics which is mainly provider-backed

## Maintenance Note

This wiki layout is publication-oriented. For sync guidance and source-of-truth notes, use [Publishing Notes](./Publishing-Notes.md).