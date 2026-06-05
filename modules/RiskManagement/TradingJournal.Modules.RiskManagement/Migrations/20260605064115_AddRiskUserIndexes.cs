using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskUserIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RiskConfigs_CreatedBy",
                schema: "Risk",
                table: "RiskConfigs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DailyRiskSnapshots_CreatedBy_SnapshotDate",
                schema: "Risk",
                table: "DailyRiskSnapshots",
                columns: new[] { "CreatedBy", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBalanceEntries_CreatedBy_EntryDate",
                schema: "Risk",
                table: "AccountBalanceEntries",
                columns: new[] { "CreatedBy", "EntryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskConfigs_CreatedBy",
                schema: "Risk",
                table: "RiskConfigs");

            migrationBuilder.DropIndex(
                name: "IX_DailyRiskSnapshots_CreatedBy_SnapshotDate",
                schema: "Risk",
                table: "DailyRiskSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_AccountBalanceEntries_CreatedBy_EntryDate",
                schema: "Risk",
                table: "AccountBalanceEntries");
        }
    }
}
