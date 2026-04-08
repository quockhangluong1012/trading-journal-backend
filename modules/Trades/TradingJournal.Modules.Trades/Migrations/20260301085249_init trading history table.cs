using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class inittradinghistorytable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Trades");

            migrationBuilder.CreateTable(
                name: "PretradeChecklists",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckListType = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PretradeChecklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalAnalyses",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingSessions",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingZones",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromTime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToTime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskGuardrails",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountEquity = table.Column<double>(type: "float", nullable: true),
                    RiskPercentage = table.Column<double>(type: "float", nullable: true),
                    MaxDailyLoss = table.Column<double>(type: "float", nullable: true),
                    TakeProfit = table.Column<double>(type: "float", nullable: true),
                    PositionSize = table.Column<double>(type: "float", nullable: true),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskGuardrails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeHistories",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Asset = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    EntryPrice = table.Column<double>(type: "float", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExitPrice = table.Column<double>(type: "float", nullable: true),
                    Pnl = table.Column<double>(type: "float", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TradingSessionId = table.Column<int>(type: "int", nullable: true),
                    TradingZoneId = table.Column<int>(type: "int", nullable: true),
                    TargetTier1 = table.Column<double>(type: "float", nullable: false),
                    TargetTier2 = table.Column<double>(type: "float", nullable: true),
                    TargetTier3 = table.Column<double>(type: "float", nullable: true),
                    StopLoss = table.Column<double>(type: "float", nullable: false),
                    RiskGuardrailId = table.Column<int>(type: "int", nullable: true),
                    ConfidenceLevel = table.Column<int>(type: "int", nullable: false),
                    PsychologyNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeHistories_RiskGuardrails_RiskGuardrailId",
                        column: x => x.RiskGuardrailId,
                        principalSchema: "Trades",
                        principalTable: "RiskGuardrails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TradeHistories_TradingSessions_TradingSessionId",
                        column: x => x.TradingSessionId,
                        principalSchema: "Trades",
                        principalTable: "TradingSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TradeEmotionTags",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: false),
                    EmotionTagId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeEmotionTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeEmotionTags_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeHistoryChecklists",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: false),
                    PretradeChecklistId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeHistoryChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeHistoryChecklists_PretradeChecklists_PretradeChecklistId",
                        column: x => x.PretradeChecklistId,
                        principalSchema: "Trades",
                        principalTable: "PretradeChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeHistoryChecklists_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeScreenshots",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeScreenshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeScreenshots_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeTechnicalAnalysisTags",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: false),
                    TechnicalAnalysisId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeTechnicalAnalysisTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeTechnicalAnalysisTags_TechnicalAnalyses_TechnicalAnalysisId",
                        column: x => x.TechnicalAnalysisId,
                        principalSchema: "Trades",
                        principalTable: "TechnicalAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeTechnicalAnalysisTags_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskGuardrails_TradeHistoryId",
                schema: "Trades",
                table: "RiskGuardrails",
                column: "TradeHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeEmotionTags_TradeHistoryId",
                schema: "Trades",
                table: "TradeEmotionTags",
                column: "TradeHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_RiskGuardrailId",
                schema: "Trades",
                table: "TradeHistories",
                column: "RiskGuardrailId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_TradingSessionId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistoryChecklists_PretradeChecklistId",
                schema: "Trades",
                table: "TradeHistoryChecklists",
                column: "PretradeChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistoryChecklists_TradeHistoryId",
                schema: "Trades",
                table: "TradeHistoryChecklists",
                column: "TradeHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeScreenshots_TradeHistoryId",
                schema: "Trades",
                table: "TradeScreenshots",
                column: "TradeHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeTechnicalAnalysisTags_TechnicalAnalysisId",
                schema: "Trades",
                table: "TradeTechnicalAnalysisTags",
                column: "TechnicalAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeTechnicalAnalysisTags_TradeHistoryId",
                schema: "Trades",
                table: "TradeTechnicalAnalysisTags",
                column: "TradeHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_RiskGuardrails_TradeHistories_TradeHistoryId",
                schema: "Trades",
                table: "RiskGuardrails",
                column: "TradeHistoryId",
                principalSchema: "Trades",
                principalTable: "TradeHistories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiskGuardrails_TradeHistories_TradeHistoryId",
                schema: "Trades",
                table: "RiskGuardrails");

            migrationBuilder.DropTable(
                name: "TradeEmotionTags",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradeHistoryChecklists",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradeScreenshots",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradeTechnicalAnalysisTags",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradingZones",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "PretradeChecklists",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TechnicalAnalyses",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradeHistories",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "RiskGuardrails",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradingSessions",
                schema: "Trades");
        }
    }
}
