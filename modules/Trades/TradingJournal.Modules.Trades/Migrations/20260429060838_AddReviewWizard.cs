using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewWizard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TradingSetupId",
                schema: "Trades",
                table: "TradeHistories",
                type: "int",
                nullable: true);

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
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionRating = table.Column<int>(type: "int", nullable: true),
                    DisciplineRating = table.Column<int>(type: "int", nullable: true),
                    PsychologyRating = table.Column<int>(type: "int", nullable: true),
                    RiskManagementRating = table.Column<int>(type: "int", nullable: true),
                    OverallRating = table.Column<int>(type: "int", nullable: true),
                    PerformanceNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BestTradeReflection = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WorstTradeReflection = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisciplineNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PsychologyNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GoalsForNextPeriod = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KeyTakeaways = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    TotalPnl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WinRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RuleBreaks = table.Column<int>(type: "int", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "ReviewActionItems",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingReviewId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewActionItems_TradingReviews_TradingReviewId",
                        column: x => x.TradingReviewId,
                        principalSchema: "Trades",
                        principalTable: "TradingReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewActionItems_TradingReviewId",
                schema: "Trades",
                table: "ReviewActionItems",
                column: "TradingReviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewActionItems",
                schema: "Trades");

            migrationBuilder.DropTable(
                name: "TradingReviews",
                schema: "Trades");

            migrationBuilder.DropColumn(
                name: "TradingSetupId",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
