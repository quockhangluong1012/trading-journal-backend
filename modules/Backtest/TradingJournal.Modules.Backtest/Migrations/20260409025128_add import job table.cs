using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Backtest.Migrations
{
    /// <inheritdoc />
    public partial class addimportjobtable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CsvImportJobs",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ImportedCandles = table.Column<int>(type: "int", nullable: false),
                    SkippedDuplicates = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsvImportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsvImportJobs_BacktestAssets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "Backtest",
                        principalTable: "BacktestAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CsvImportJobs_AssetId",
                schema: "Backtest",
                table: "CsvImportJobs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CsvImportJobs_StatusCreated",
                schema: "Backtest",
                table: "CsvImportJobs",
                columns: new[] { "Status", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CsvImportJobs",
                schema: "Backtest");
        }
    }
}
