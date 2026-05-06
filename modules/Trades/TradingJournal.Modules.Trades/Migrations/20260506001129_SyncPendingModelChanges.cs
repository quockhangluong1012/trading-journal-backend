using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);
        }
    }
}
