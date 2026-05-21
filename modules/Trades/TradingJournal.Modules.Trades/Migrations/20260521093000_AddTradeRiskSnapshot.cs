using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeRiskSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AccountBalanceAtEntry",
                schema: "Trades",
                table: "TradeHistories",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskAmountAtEntry",
                schema: "Trades",
                table: "TradeHistories",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskRewardRatioAtEntry",
                schema: "Trades",
                table: "TradeHistories",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedPositionLots",
                schema: "Trades",
                table: "TradeHistories",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedPositionUnits",
                schema: "Trades",
                table: "TradeHistories",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountBalanceAtEntry",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "RiskAmountAtEntry",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "RiskRewardRatioAtEntry",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "SuggestedPositionLots",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "SuggestedPositionUnits",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
