# Trading Journal

TradingJournal is a comprehensive **Trading Analysis Platform** built with .NET and React. It provides traders with tools to log trades, track performance, manage risk, and analyze their trading psychology.

## 🚀 Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20.9+](https://nodejs.org/)
- [npm](https://www.npmjs.com/)
- [SQL Server](https://www.microsoft.com/sql-server) or SQL Server Express/LocalDB

### Backend Setup

1.  **Clone the repository**
    ```bash
    git clone <repository-url>
    cd trading-journal-backend
    ```

2.  **Restore NuGet packages**
    ```bash
    dotnet restore
    ```

3.  **Configure the database**
        For local `Development` runs, set `ConnectionStrings__TradeDatabase` via environment variables/user secrets or update `bootstrapper/TradingJournal.ApiGateWay/appsettings.Development.json`. The default launch profiles use `ASPNETCORE_ENVIRONMENT=Development`, so `appsettings.Development.json` overrides `appsettings.json`.
    ```json
    {
      "ConnectionStrings": {
        "TradeDatabase": "Server=localhost;Database=TradingJournal;Trusted_Connection=True;TrustServerCertificate=True"
      }
    }
    ```

4.  **Run the application**
    ```bash
    dotnet run --project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
    ```
    In `Development`, the host applies several module migrations automatically on startup. The current startup path does not include the Auth module, so on a fresh database you may still need to apply Auth migrations manually using the module-specific EF Core commands described in `CLAUDE.md`.
    The API will be available at `http://localhost:5226` and `https://localhost:7177`.

### Frontend Setup

The frontend lives in a separate checkout. Clone or open `trading-journal-ui` alongside this backend repository before running these steps.

1.  **Navigate to the frontend directory**
    ```bash
    cd ../trading-journal-ui
    ```

2.  **Install dependencies**
    ```bash
    npm install
    ```

3.  **Configure environment variables**
    Update `.env.development` in the UI repo and set the backend origin. The UI appends `/api` itself, so do not include `/api` in the value:
    ```env
    NEXT_PUBLIC_API_URL=https://localhost:7177
    ```

4.  **Run the application**
    ```bash
    npm run dev
    ```
    The frontend will be available at `http://localhost:3000`

## 📚 Documentation

- [Backend Docs Index](./docs/README.md) - Fast orientation, module map, and reading paths inside the repository
- [GitHub Wiki Staging Layout](./docs/wiki/Home.md) - Home page, sidebar, and page structure ready to copy into a GitHub Wiki
- [Technical Spec](./docs/TECHNICAL_SPEC.md) - Canonical backend architecture and platform details
- [Code Flow](./docs/CODE_FLOW.md) - Startup, request, event, and hosted-service execution flow
- [Feature Flow](./docs/FEATURE_FLOW.md) - End-to-end backend journeys across modules

## 🏗️ Project Structure

```
trading-journal-backend/
├── bootstrapper/
│   └── TradingJournal.ApiGateWay/           # ASP.NET Core host and composition root
├── modules/                                 # Business modules
│   ├── Auth/
│   ├── Trades/
│   ├── Psychology/
│   ├── Analytics/
│   ├── TradingSetup/
│   ├── AiInsights/
│   ├── Notifications/
│   ├── Scanner/
│   └── RiskManagement/
├── shared/                                  # Cross-cutting abstractions and infrastructure
├── tests/                                   # xUnit test projects by module
└── docs/                                    # Backend documentation set
```

## 🛠️ Tech Stack

### Backend
- [.NET 10.0](https://dotnet.microsoft.com/)
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- [EF Core](https://docs.microsoft.com/en-us/ef/)
- [MediatR](https://github.com/jbogard/MediatR)
- [Carter](https://github.com/CarterCommunity/Carter)
- [SQL Server](https://www.microsoft.com/sql-server)
- [JWT Authentication](https://jwt.io/)
- [SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction)
- [Serilog](https://serilog.net/)

### Frontend
- [React 19](https://react.dev/)
- [Next.js 16](https://nextjs.org/)
- [TypeScript](https://www.typescriptlang.org/)
- [Zustand](https://github.com/pmndrs/zustand)
- [Tailwind CSS](https://tailwindcss.com/)
- [shadcn/ui](https://ui.shadcn.com/)
- [Radix UI](https://www.radix-ui.com/)

## 🧪 Testing

### Backend Tests

Run tests with:

```bash
dotnet test tests/TradingJournal.Tests.Auth/TradingJournal.Tests.Auth.csproj
dotnet test tests/TradingJournal.Tests.Trades/TradingJournal.Tests.Trades.csproj
dotnet test tests/TradingJournal.Tests.Analytics/TradingJournal.Tests.Analytics.csproj
dotnet test tests/TradingJournal.Tests.Psychology/TradingJournal.Tests.Psychology.csproj
dotnet test tests/TradingJournal.Tests.Scanner/TradingJournal.Tests.Scanner.csproj
dotnet test tests/TradingJournal.Tests.Integration/TradingJournal.Tests.Integration.csproj
```

### Frontend Tests

Run tests with:

```bash
npm run test
```

## 📂 Code Organization

### Backend Module Architecture

The backend is a modular monolith with a single host in `bootstrapper/TradingJournal.ApiGateWay`. Each module owns its own handlers, validators, registrations, and persistence boundary, while shared infrastructure lives under `shared/`.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.