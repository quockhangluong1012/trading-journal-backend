using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Backtest.Migrations
{
    /// <inheritdoc />
    public partial class AddLeverageAndMarginToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Leverage",
                schema: "Backtest",
                table: "BacktestSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintenanceMarginPercentage",
                schema: "Backtest",
                table: "BacktestSessions",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Leverage",
                schema: "Backtest",
                table: "BacktestSessions");

            migrationBuilder.DropColumn(
                name: "MaintenanceMarginPercentage",
                schema: "Backtest",
                table: "BacktestSessions");
        }
    }
}
