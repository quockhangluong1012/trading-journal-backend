using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class addAItradesummarytable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TradingSummaries",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeId = table.Column<int>(type: "int", nullable: false),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalInsights = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PsychologyAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    CriticalMistakes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingSummaries_TradeHistories_TradeId",
                        column: x => x.TradeId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_TradingZoneId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingSummaries_TradeId",
                schema: "Trades",
                table: "TradingSummaries",
                column: "TradeId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_TradingZones_TradingZoneId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingZoneId",
                principalSchema: "Trades",
                principalTable: "TradingZones",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_TradingZones_TradingZoneId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropTable(
                name: "TradingSummaries",
                schema: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_TradingZoneId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
