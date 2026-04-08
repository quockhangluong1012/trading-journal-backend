using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class adddescriptionandshortnamecolumntoTechnicalAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeTechnicalAnalysisTags_TechnicalAnalyses_TechnicalAnalysisId",
                schema: "Trades",
                table: "TradeTechnicalAnalysisTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TechnicalAnalyses",
                schema: "Trades",
                table: "TechnicalAnalyses");

            migrationBuilder.RenameTable(
                name: "TechnicalAnalyses",
                schema: "Trades",
                newName: "TechnicalAnalysis",
                newSchema: "Trades");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "Trades",
                table: "TechnicalAnalysis",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                schema: "Trades",
                table: "TechnicalAnalysis",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TechnicalAnalysis",
                schema: "Trades",
                table: "TechnicalAnalysis",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeTechnicalAnalysisTags_TechnicalAnalysis_TechnicalAnalysisId",
                schema: "Trades",
                table: "TradeTechnicalAnalysisTags",
                column: "TechnicalAnalysisId",
                principalSchema: "Trades",
                principalTable: "TechnicalAnalysis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradeTechnicalAnalysisTags_TechnicalAnalysis_TechnicalAnalysisId",
                schema: "Trades",
                table: "TradeTechnicalAnalysisTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TechnicalAnalysis",
                schema: "Trades",
                table: "TechnicalAnalysis");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "Trades",
                table: "TechnicalAnalysis");

            migrationBuilder.DropColumn(
                name: "ShortName",
                schema: "Trades",
                table: "TechnicalAnalysis");

            migrationBuilder.RenameTable(
                name: "TechnicalAnalysis",
                schema: "Trades",
                newName: "TechnicalAnalyses",
                newSchema: "Trades");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TechnicalAnalyses",
                schema: "Trades",
                table: "TechnicalAnalyses",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TradeTechnicalAnalysisTags_TechnicalAnalyses_TechnicalAnalysisId",
                schema: "Trades",
                table: "TradeTechnicalAnalysisTags",
                column: "TechnicalAnalysisId",
                principalSchema: "Trades",
                principalTable: "TechnicalAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
