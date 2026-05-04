using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Scanner.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketRegimeToScannerAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Regime",
                schema: "Scanner",
                table: "ScannerAlerts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Regime",
                schema: "Scanner",
                table: "ScannerAlerts");
        }
    }
}
