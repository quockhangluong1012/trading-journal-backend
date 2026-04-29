using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonsAndDiscipline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeHistories_TradingSummaries_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropTable(
                name: "SetupConnections",
                schema: "Setups");

            migrationBuilder.DropTable(
                name: "TradingReviews",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradingSummaries",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "SetupSteps",
                schema: "Setups");

            migrationBuilder.DropTable(
                name: "TradingSetups",
                schema: "Setups");

            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.CreateTable(
                name: "DisciplineRules",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplineRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LessonsLearned",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    KeyTakeaway = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActionItems = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImpactScore = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonsLearned", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisciplineLogs",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisciplineRuleId = table.Column<int>(type: "int", nullable: false),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: true),
                    WasFollowed = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplineLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisciplineLogs_DisciplineRules_DisciplineRuleId",
                        column: x => x.DisciplineRuleId,
                        principalSchema: "Trades",
                        principalTable: "DisciplineRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisciplineLogs_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LessonTradeLinks",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonLearnedId = table.Column<int>(type: "int", nullable: false),
                    TradeHistoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonTradeLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonTradeLinks_LessonsLearned_LessonLearnedId",
                        column: x => x.LessonLearnedId,
                        principalSchema: "Trades",
                        principalTable: "LessonsLearned",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonTradeLinks_TradeHistories_TradeHistoryId",
                        column: x => x.TradeHistoryId,
                        principalSchema: "Trades",
                        principalTable: "TradeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplineLogs_DisciplineRuleId",
                schema: "Trades",
                table: "DisciplineLogs",
                column: "DisciplineRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplineLogs_TradeHistoryId",
                schema: "Trades",
                table: "DisciplineLogs",
                column: "TradeHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonTradeLinks_LessonLearnedId",
                schema: "Trades",
                table: "LessonTradeLinks",
                column: "LessonLearnedId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonTradeLinks_TradeHistoryId",
                schema: "Trades",
                table: "LessonTradeLinks",
                column: "TradeHistoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisciplineLogs",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "LessonTradeLinks",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "DisciplineRules",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "LessonsLearned",
                schema: "Trades");

            migrationBuilder.EnsureSchema(
                name: "Setups");

            migrationBuilder.CreateTable(
                name: "TradingReviews",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AiActionItems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiCriticalMistakesPsychological = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiCriticalMistakesTechnical = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiPsychologyAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiSummaryGenerating = table.Column<bool>(type: "bit", nullable: false),
                    AiTechnicalInsights = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiWeaknesses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiWhatToImprove = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    TotalPnl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WinRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingSetups",
                schema: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSetups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingSummaries",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    PsychologyAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalInsights = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "SetupSteps",
                schema: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingSetupId = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NodeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionX = table.Column<double>(type: "float", nullable: false),
                    PositionY = table.Column<double>(type: "float", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupSteps_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalSchema: "Setups",
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetupConnections",
                schema: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceStepId = table.Column<int>(type: "int", nullable: false),
                    TargetStepId = table.Column<int>(type: "int", nullable: false),
                    TradingSetupId = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAnimated = table.Column<bool>(type: "bit", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupConnections_SetupSteps_SourceStepId",
                        column: x => x.SourceStepId,
                        principalSchema: "Setups",
                        principalTable: "SetupSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupConnections_SetupSteps_TargetStepId",
                        column: x => x.TargetStepId,
                        principalSchema: "Setups",
                        principalTable: "SetupSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupConnections_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalSchema: "Setups",
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupConnections_SourceStepId",
                schema: "Setups",
                table: "SetupConnections",
                column: "SourceStepId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupConnections_TargetStepId",
                schema: "Setups",
                table: "SetupConnections",
                column: "TargetStepId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupConnections_TradingSetupId",
                schema: "Setups",
                table: "SetupConnections",
                column: "TradingSetupId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupSteps_TradingSetupId",
                schema: "Setups",
                table: "SetupSteps",
                column: "TradingSetupId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingSummaries_TradeId",
                schema: "Trades",
                table: "TradingSummaries",
                column: "TradeId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeHistories_TradingSummaries_TradingSummaryId",
                schema: "Trades",
                table: "TradeHistories",
                column: "TradingSummaryId",
                principalSchema: "Trades",
                principalTable: "TradingSummaries",
                principalColumn: "Id");
        }
    }
}
