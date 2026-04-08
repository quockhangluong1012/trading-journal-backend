using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddHitStopLoss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PsychologyNotes",
                schema: "Trades",
                table: "TradeHistories",
                newName: "TradingResult");

            migrationBuilder.AddColumn<bool>(
                name: "HitStopLoss",
                schema: "Trades",
                table: "TradeHistories",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HitStopLoss",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.RenameColumn(
                name: "TradingResult",
                schema: "Trades",
                table: "TradeHistories",
                newName: "PsychologyNotes");
        }
    }
}
