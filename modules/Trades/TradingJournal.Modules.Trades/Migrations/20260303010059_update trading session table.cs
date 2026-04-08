using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class updatetradingsessiontable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartTime",
                schema: "Trades",
                table: "TradingSessions",
                newName: "FromTime");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                schema: "Trades",
                table: "TradingSessions",
                newName: "ToTime");

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                schema: "Trades",
                table: "TradingSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "Trades",
                table: "TradingSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PnL",
                schema: "Trades",
                table: "TradingSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TradeCount",
                schema: "Trades",
                table: "TradingSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                schema: "Trades",
                table: "TradingSessions");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "Trades",
                table: "TradingSessions");

            migrationBuilder.DropColumn(
                name: "PnL",
                schema: "Trades",
                table: "TradingSessions");

            migrationBuilder.DropColumn(
                name: "TradeCount",
                schema: "Trades",
                table: "TradingSessions");

            migrationBuilder.RenameColumn(
                name: "ToTime",
                schema: "Trades",
                table: "TradingSessions",
                newName: "EndTime");

            migrationBuilder.RenameColumn(
                name: "FromTime",
                schema: "Trades",
                table: "TradingSessions",
                newName: "StartTime");
        }
    }
}
