using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Backtest.Migrations
{
    /// <inheritdoc />
    public partial class DropDuplicateBacktestIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OhlcvCandles_Lookup",
                schema: "Backtest",
                table: "OhlcvCandles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OhlcvCandles_Lookup",
                schema: "Backtest",
                table: "OhlcvCandles",
                columns: new[] { "Asset", "Timeframe", "Timestamp" });
        }
    }
}
