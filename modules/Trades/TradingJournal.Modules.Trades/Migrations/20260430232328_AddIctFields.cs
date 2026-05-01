using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddIctFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyBias",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarketStructure",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PowerOf3Phase",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PremiumDiscount",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyBias",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "MarketStructure",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "PowerOf3Phase",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "PremiumDiscount",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
