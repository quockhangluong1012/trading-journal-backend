using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Backtest.Migrations
{
    /// <inheritdoc />
    public partial class addbacktestingmodule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Backtest");

            migrationBuilder.CreateTable(
                name: "BacktestAssets",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SyncStatus = table.Column<int>(type: "int", nullable: false),
                    DataProvider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DataStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalCandles = table.Column<long>(type: "bigint", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BacktestSessions",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Asset = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActiveTimeframe = table.Column<int>(type: "int", nullable: false),
                    PlaybackSpeed = table.Column<int>(type: "int", nullable: false),
                    IsDataReady = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OhlcvCandles",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Asset = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timeframe = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    High = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    Low = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    Close = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OhlcvCandles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BacktestOrders",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    OrderType = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    FilledPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: true),
                    PositionSize = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(28,10)", nullable: true),
                    TakeProfit = table.Column<decimal>(type: "decimal(28,10)", nullable: true),
                    ExitPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: true),
                    Pnl = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    OrderedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestOrders_BacktestSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "Backtest",
                        principalTable: "BacktestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChartDrawings",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    DrawingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartDrawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChartDrawings_BacktestSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "Backtest",
                        principalTable: "BacktestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BacktestTradeResults",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    PositionSize = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Pnl = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EntryTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestTradeResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestTradeResults_BacktestOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "Backtest",
                        principalTable: "BacktestOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BacktestTradeResults_BacktestSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "Backtest",
                        principalTable: "BacktestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestAssets_Symbol",
                schema: "Backtest",
                table: "BacktestAssets",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestOrders_SessionId",
                schema: "Backtest",
                table: "BacktestOrders",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTradeResults_OrderId",
                schema: "Backtest",
                table: "BacktestTradeResults",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTradeResults_SessionId",
                schema: "Backtest",
                table: "BacktestTradeResults",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartDrawings_SessionId",
                schema: "Backtest",
                table: "ChartDrawings",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OhlcvCandles_AssetTimeframeTimestamp",
                schema: "Backtest",
                table: "OhlcvCandles",
                columns: new[] { "Asset", "Timeframe", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OhlcvCandles_Lookup",
                schema: "Backtest",
                table: "OhlcvCandles",
                columns: new[] { "Asset", "Timeframe", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestAssets",
                schema: "Backtest");

            migrationBuilder.DropTable(
                name: "BacktestTradeResults",
                schema: "Backtest");

            migrationBuilder.DropTable(
                name: "ChartDrawings",
                schema: "Backtest");

            migrationBuilder.DropTable(
                name: "OhlcvCandles",
                schema: "Backtest");

            migrationBuilder.DropTable(
                name: "BacktestOrders",
                schema: "Backtest");

            migrationBuilder.DropTable(
                name: "BacktestSessions",
                schema: "Backtest");
        }
    }
}
