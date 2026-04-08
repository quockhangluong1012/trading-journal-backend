# Role & Context
You are an Expert Full-Stack Software Engineer, System Architect, and FinTech Specialist. Your objective is to design, architect, and implement a comprehensive **Trading Backtesting Platform**.

You will provide the system architecture, database schema, API design, and step-by-step code implementation (Frontend and Backend) based on the detailed specifications below.

---

# 1. Technology Stack & Architecture Constraints
- **Backend Framework:** .NET 10 (C# 14), ASP.NET Core Web API (Minimal APIs).
- **Architecture Pattern:** Vertical Slice Architecture utilizing CQRS (via MediatR). Code must be grouped by domain features (e.g., `Features/Sessions`, `Features/Orders`, `Features/MarketData`).
- **Relational Database:** SQL Server or PostgreSQL for core structured data (Users, Sessions, Order Configurations, Analytics). Use Entity Framework Core.
- **NoSQL Database:** MongoDB (Highly recommended for storing complex, schema-less JSON payloads of Chart Drawings and high-speed retrieval of OHLCV historical market data).
- **Messaging & Background Jobs:** RabbitMQ or Azure Service Bus integrated with a `.NET BackgroundService` (IHostedService) for Market Data ingestion.
- **Real-time Communication:** SignalR / WebSockets for streaming playback data and job progress updates to the UI.
- **Frontend Framework:** React (Next.js) or Vue 3 / Angular with TypeScript.
- **Charting Library:** TradingView Advanced Charting Library, Lightweight Charts, or Apache ECharts.

---

# 2. Frontend Requirements (UI/UX & Business Logic)

### 2.1. Backtest Dashboard ("Backtest" Tab)
- **Navigation:** A dedicated primary navigation tab named "Backtest".
- **Session List:** A data grid displaying all user backtest sessions.
  - *Columns:* Session ID, Asset Pair, Date Range, Initial Balance, Current Balance, PnL (%), and Status (In-Progress, Completed, Liquidated).
  - *Actions:* "Resume" (redirects to active chart), "View Details" (redirects to results page), and "Delete".

### 2.2. Create New Session Modal
- **Inputs:**
  - Asset Selection (Searchable dropdown, e.g., BTC/USDT, AAPL).
  - Start Date (End date defaults to "Present").
  - Initial Balance (e.g., $10,000).
- **Validation:** Initial Balance must be > 0.
- **Flow:** Clicking "Create" sends a command to the backend, triggers the Market Data job, and redirects the user to the Active Backtest Workspace.

### 2.3. Active Backtest Workspace (The Replay Engine)
- **Historical Chart:** A candlestick chart loading historical OHLCV (Open, High, Low, Close, Volume) data starting exactly from the chosen Start Date.
- **Playback Controls:**
  - **Play:** Automatically fetches and renders the next candle at a configurable interval (include speed multipliers like 1x, 2x, 5x).
  - **Pause:** Halts automatic rendering.
  - **Skip/Next:** Manually advances the chart exactly one candle forward.
- **Multi-Timeframe (MTF) Support:**
  - Dropdown to switch between 1M,5M, 15M, 1H, 4H, and 1D.
  - *CRITICAL RULE (Look-ahead bias prevention):* When switching timeframes (e.g., from 15M up to 1H), the chart must strictly calculate and display candles up to the *current simulated timestamp*. It must never reveal "future" price action before the user plays it.

### 2.4. Advanced Charting & Drawing Tools (TradingView Clone)
- **Toolbox:** A sidebar containing drawing tools: Pen (free draw), Rectangle (Zone marking), Fibonacci Retracement, Gann Box, Trend Line, and Path.
- **Object Manipulation:** Every drawing is an independent, selectable object. Users can click to select, drag to move, resize, or hit `Delete` to remove.
  - *Technical Constraint:* Drawings must be anchored to **Time and Price** coordinates (not X/Y screen pixels) so they scale correctly when zooming or changing timeframes.
- **Customization:** Double-clicking an object allows users to edit: text overlays, text placement, font size, stroke color/thickness, and background fill color/opacity.
- **Persistence:** Every drawing must be serialized to a JSON array and auto-saved to MongoDB/PostgreSQL so they persist upon session resumption.

### 2.5. Order Management & Trading Engine
- **Order Panel:** UI to place Market and Limit orders. Includes inputs for Position Size, Direction (Long/Short), Stop Loss (SL), and Take Profit (TP).
- **Simulated Execution Logic:**
  - **Market Orders:** Executed immediately at the closing price of the *currently rendered* playback candle.
  - **Limit Orders:** Created with an initial state of `Pending`. As new candles render (via Play or Skip), the backend engine evaluates the `High` and `Low` of the *new* candle. If the price action intersects the Limit Entry Price, the order state changes to `Active`.
- **Margin & Liquidation Rule:** On every tick/candle update, calculate the user's `Equity` (Balance + Unrealized PnL). If `Equity <= 0`, forcefully close all positions, cancel pending orders, update the session status to `Liquidated`, and halt the playback.

### 2.6. Post-Session Results & Analytics Page
- **Trigger:** Shown when a session is finished manually, liquidated, or when clicking "View Details" from the dashboard.
- **Part 1 (Visual Chart):** A static historical chart displaying the entire tested timeline. It must overlay markers (e.g., green/red arrows or connected lines) showing exactly where every order entry and exit occurred.
- **Part 2 (Analytics Dashboard):**
  - *Equity Curve:* A line chart showing account balance over time.
  - *Summary Metrics:* Total Trades, Win Rate (%), Total Wins, Total Losses, Gross Profit, Gross Loss, Net PnL, Max Drawdown.
  - *Trade Log Table:* Detailed ledger of all executed orders (Entry/Exit Time, Side, Size, Entry/Exit Price, Individual PnL).

---

# 3. Backend Core Logic & Architecture

### 3.1. MarketData Background Service
- **Trigger:** When a session is created, the API publishes a `FetchHistoricalDataCommand` to the message broker.
- **Worker:** A `.NET BackgroundService` consumes the message, connects to a market data provider (e.g., Binance, Polygon.io), and downloads the OHLCV data.
- **Storage & Notification:** The worker normalizes the data, saves it to MongoDB for ultra-fast sequential read access by the playback engine, and uses SignalR to notify the frontend when data is ready.

### 3.2. State Management & Resumption
- **State Saving:** The backend must continuously track and persist the current playback timestamp, open positions, pending orders, serialized drawings, and account balance.
- **Resuming:** When a user resumes a session, the API must return the exact state, restoring the chart timeline, active trades, and drawings perfectly.

---

# 4. Execution Instructions for the AI
Please acknowledge these requirements. **Do not write the entire application at once**, as it will exceed context limits. We will proceed step-by-step. 

For your first response, please execute **Step 1**:

1. **Architecture & Database Schema:** Provide a file-tree layout demonstrating the Vertical Slice Architecture for this .NET 10 project. Next, provide the Entity Framework Core models (`Session`, `Order`, `TradeResult`) and the NoSQL/JSON structure for `ChartDrawings`.
2. **Core Matching Engine Logic (C#):** Write the specific C# logic/service that evaluates an incoming candle (OHLC) against a list of pending limit orders to determine if they trigger.
3. **MarketData Worker Skeleton (C#):** Provide the code for the `.NET BackgroundService` that handles the message queue for downloading historical data.
4. **Frontend State Management Strategy:** Briefly explain how you will manage the complex state of chart playback, multi-timeframe switching, and drawing synchronizations using a modern state manager (like Redux, Zustand, or Pinia).

Awaiting your first step.