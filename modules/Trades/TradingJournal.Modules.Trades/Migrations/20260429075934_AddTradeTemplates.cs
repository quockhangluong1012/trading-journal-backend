using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeTemplates",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Asset = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: true),
                    TradingZoneId = table.Column<int>(type: "int", nullable: true),
                    TradingSessionId = table.Column<int>(type: "int", nullable: true),
                    TradingSetupId = table.Column<int>(type: "int", nullable: true),
                    DefaultStopLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DefaultTargetTier1 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DefaultTargetTier2 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DefaultTargetTier3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DefaultConfidenceLevel = table.Column<int>(type: "int", nullable: true),
                    DefaultNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultChecklistIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultEmotionTagIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultTechnicalAnalysisTagIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeTemplates_TradingSessions_TradingSessionId",
                        column: x => x.TradingSessionId,
                        principalSchema: "Trades",
                        principalTable: "TradingSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TradeTemplates_TradingZones_TradingZoneId",
                        column: x => x.TradingZoneId,
                        principalSchema: "Trades",
                        principalTable: "TradingZones",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeTemplates_TradingSessionId",
                schema: "Trades",
                table: "TradeTemplates",
                column: "TradingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeTemplates_TradingZoneId",
                schema: "Trades",
                table: "TradeTemplates",
                column: "TradingZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeTemplates",
                schema: "Trades");
        }
    }
}
