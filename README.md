# Trading Journal

TradingJournal is a comprehensive **Trading Analysis Platform** built with .NET and React. It provides traders with tools to log trades, track performance, manage risk, and analyze their trading psychology.

## 🚀 Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)
- [npm](https://www.npmjs.com/)
- [PostgreSQL 13+](https://www.postgresql.org/)

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
    Update the connection string in `appsettings.json` or use environment variables.
    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=tradingjournal;Username=postgres;Password=[PASSWORD]"
      }
    }
    ```

4.  **Apply migrations**
    ```bash
    dotnet ef database update --project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
    ```

5.  **Seed data (optional)**
    ```bash
    dotnet run --project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj -- --seed
    ```

6.  **Run the application**
    ```bash
    dotnet run --project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
    ```
    The API will be available at `http://localhost:5000`

### Frontend Setup

1.  **Navigate to the frontend directory**
    ```bash
    cd trading-journal-ui
    ```

2.  **Install dependencies**
    ```bash
    npm install
    ```

3.  **Configure environment variables**
    Copy `.env.local.example` to `.env.local` and update the API URL:
    ```env
    NEXT_PUBLIC_API_URL=http://localhost:5000/api
    ```

4.  **Run the application**
    ```bash
    npm run dev
    ```
    The frontend will be available at `http://localhost:3000`

## 📚 Documentation

- [API Documentation](./docs/API_DOCUMENTATION.md) - Detailed API endpoints and usage examples
- [Architecture](./docs/ARCHITECTURE.md) - System architecture and design decisions
- [Database Schema](./docs/DATABASE_SCHEMA.md) - Entity relationships and data model
- [Testing Guide](./docs/TESTING_GUIDE.md) - How to run tests and validation procedures

## 🏗️ Project Structure

```
trading-journal-backend/
├── src/
│   ├── TradingJournal.Abstractions/          # Shared interfaces and base types
│   ├── TradingJournal.Application/           # Business logic and use cases
│   ├── TradingJournal.Database/              # EF Core configuration and migrations
│   ├── TradingJournal.Domain/                # Domain entities and aggregates
│   ├── TradingJournal.Infrastructure/          # External integrations and services
│   ├── TradingJournal.Modules/               # Modular domain services
│   ├── TradingJournal.ApiGateWay/           # API Gateway and host
│   └── bootstrapper/                       # Deployment scripts and tools
├── docs/                                     # Documentation
└── .env.example                              # Environment variables template
```

## 🛠️ Tech Stack

### Backend
- [.NET 10.0](https://dotnet.microsoft.com/)
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- [EF Core](https://docs.microsoft.com/en-us/ef/)
- [MediatR](https://github.com/jbogard/MediatR)
- [AutoMapper](https://github.com/AutoMapper/AutoMapper)
- [PostgreSQL](https://www.postgresql.org/)
- [JWT Authentication](https://jwt.io/)
- [Azure AD Integration](https://azure.microsoft.com/en-us/services/active-directory/)

### Frontend
- [React 18](https://reactjs.org/)
- [Next.js 14](https://nextjs.org/)
- [TypeScript](https://www.typescriptlang.org/)
- [Zustand](https://github.com/pmndrs/zustand)
- [Tailwind CSS](https://tailwindcss.com/)
- [shadcn/ui](https://ui.shadcn.com/)
- [Radix UI](https://www.radix-ui.com/)

## 🧪 Testing

### Backend Tests

Run tests with:

```bash
dotnet test --project bootstrapper/TradingJournal.ApiGateWay/TradingJournal.ApiGateWay.csproj
```

### Frontend Tests

Run tests with:

```bash
npm run test
```

## 📂 Code Organization

### Backend Module Architecture

The backend uses a modular architecture where each domain has its own:
- **Domain**: Entities, aggregates, and domain events
- **Application**: Business logic, commands, queries, and use cases
- **Infrastructure**: External integrations, repositories, and services
- **Database**: EF Core DbContext and migrations

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.