using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Scanner.Migrations
{
    /// <inheritdoc />
    public partial class InitialScannerModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Scanner");

            migrationBuilder.CreateTable(
                name: "ScannerAlerts",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PatternType = table.Column<int>(type: "int", nullable: false),
                    Timeframe = table.Column<int>(type: "int", nullable: false),
                    DetectionTimeframe = table.Column<int>(type: "int", nullable: false),
                    PriceAtDetection = table.Column<decimal>(type: "decimal(28,10)", nullable: false),
                    ZoneHighPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: true),
                    ZoneLowPrice = table.Column<decimal>(type: "decimal(28,10)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConfluenceScore = table.Column<int>(type: "int", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannerAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScannerConfigs",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ScanIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    MinConfluenceScore = table.Column<int>(type: "int", nullable: false),
                    IsRunning = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannerConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Watchlists",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScannerConfigPatterns",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScannerConfigId = table.Column<int>(type: "int", nullable: false),
                    PatternType = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannerConfigPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScannerConfigPatterns_ScannerConfigs_ScannerConfigId",
                        column: x => x.ScannerConfigId,
                        principalSchema: "Scanner",
                        principalTable: "ScannerConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScannerConfigTimeframes",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScannerConfigId = table.Column<int>(type: "int", nullable: false),
                    Timeframe = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannerConfigTimeframes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScannerConfigTimeframes_ScannerConfigs_ScannerConfigId",
                        column: x => x.ScannerConfigId,
                        principalSchema: "Scanner",
                        principalTable: "ScannerConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistAssets",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WatchlistId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchlistAssets_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalSchema: "Scanner",
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScannerAlerts_Dedup",
                schema: "Scanner",
                table: "ScannerAlerts",
                columns: new[] { "UserId", "Symbol", "PatternType", "DetectionTimeframe", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScannerAlerts_UserDetectedAt",
                schema: "Scanner",
                table: "ScannerAlerts",
                columns: new[] { "UserId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScannerConfigPatterns_ConfigPattern",
                schema: "Scanner",
                table: "ScannerConfigPatterns",
                columns: new[] { "ScannerConfigId", "PatternType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScannerConfigs_UserId",
                schema: "Scanner",
                table: "ScannerConfigs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScannerConfigTimeframes_ConfigTimeframe",
                schema: "Scanner",
                table: "ScannerConfigTimeframes",
                columns: new[] { "ScannerConfigId", "Timeframe" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistAssets_WatchlistSymbol",
                schema: "Scanner",
                table: "WatchlistAssets",
                columns: new[] { "WatchlistId", "Symbol" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScannerAlerts",
                schema: "Scanner");

            migrationBuilder.DropTable(
                name: "ScannerConfigPatterns",
                schema: "Scanner");

            migrationBuilder.DropTable(
                name: "ScannerConfigTimeframes",
                schema: "Scanner");

            migrationBuilder.DropTable(
                name: "WatchlistAssets",
                schema: "Scanner");

            migrationBuilder.DropTable(
                name: "ScannerConfigs",
                schema: "Scanner");

            migrationBuilder.DropTable(
                name: "Watchlists",
                schema: "Scanner");
        }
    }
}
