using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRiskGuardrails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_RiskGuardrails_RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropTable(
                name: "RiskGuardrails",
                schema: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RiskGuardrails",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: true),
                    AccountEquity = table.Column<double>(type: "float", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxDailyLoss = table.Column<double>(type: "float", nullable: true),
                    PositionSize = table.Column<double>(type: "float", nullable: true),
                    RiskPercentage = table.Column<double>(type: "float", nullable: true),
                    TakeProfit = table.Column<double>(type: "float", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskGuardrails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskGuardrails_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories",
                column: "RiskGuardrailId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskGuardrails_TradeHistoryId",
                schema: "Trades",
                table: "RiskGuardrails",
                column: "TradeHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_RiskGuardrails_RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories",
                column: "RiskGuardrailId",
                principalSchema: "Trades",
                principalTable: "RiskGuardrails",
                principalColumn: "Id");
        }
    }
}
