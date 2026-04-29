using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Scanner.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistIsScannerRunning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScannerRunning",
                schema: "Scanner",
                table: "Watchlists",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsScannerRunning",
                schema: "Scanner",
                table: "Watchlists");
        }
    }
}
