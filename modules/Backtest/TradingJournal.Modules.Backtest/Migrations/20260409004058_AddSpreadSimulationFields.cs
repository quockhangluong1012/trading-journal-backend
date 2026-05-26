using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Backtest.Migrations
{
    /// <inheritdoc />
    public partial class AddSpreadSimulationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Spread",
                schema: "Backtest",
                table: "BacktestSessions",
                type: "decimal(28,10)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultSpreadPips",
                schema: "Backtest",
                table: "BacktestAssets",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PipSize",
                schema: "Backtest",
                table: "BacktestAssets",
                type: "decimal(18,10)",
                nullable: false,
                defaultValue: 0.0001m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Spread",
                schema: "Backtest",
                table: "BacktestSessions");

            migrationBuilder.DropColumn(
                name: "DefaultSpreadPips",
                schema: "Backtest",
                table: "BacktestAssets");

            migrationBuilder.DropColumn(
                name: "PipSize",
                schema: "Backtest",
                table: "BacktestAssets");
        }
    }
}
