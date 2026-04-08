using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class addtradingreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingReviews",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiWeaknesses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiActionItems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalPnl = table.Column<double>(type: "float", nullable: false),
                    WinRate = table.Column<double>(type: "float", nullable: false),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingSummaryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_TradingSummaries_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingSummaryId",
                principalSchema: "Trades",
                principalTable: "TradingSummaries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_TradingSummaries_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropTable(
                name: "TradingReviews",
                schema: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
