using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeHistoryUserDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_CreatedBy_Date",
                schema: "Trades",
                table: "TradeHistories",
                columns: new[] { "CreatedBy", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_CreatedBy_Date",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
