using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInsightsColumnsToTradingReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiActionItems",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiCriticalMistakesPsychological",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiCriticalMistakesTechnical",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiPsychologyAnalysis",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiStrengths",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AiSummaryGenerating",
                schema: "Trades",
                table: "TradingReviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AiTechnicalInsights",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiWeaknesses",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiWhatToImprove",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserNotes",
                schema: "Trades",
                table: "TradingReviews",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiActionItems",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiCriticalMistakesPsychological",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiCriticalMistakesTechnical",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiPsychologyAnalysis",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiStrengths",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiSummaryGenerating",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiTechnicalInsights",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiWeaknesses",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "AiWhatToImprove",
                schema: "Trades",
                table: "TradingReviews");

            migrationBuilder.DropColumn(
                name: "UserNotes",
                schema: "Trades",
                table: "TradingReviews");
        }
    }
}
