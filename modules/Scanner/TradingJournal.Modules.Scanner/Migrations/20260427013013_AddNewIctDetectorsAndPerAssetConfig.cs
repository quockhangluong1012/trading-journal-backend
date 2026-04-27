using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Scanner.Migrations
{
    /// <inheritdoc />
    public partial class AddNewIctDetectorsAndPerAssetConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchlistAssetDetectors",
                schema: "Scanner",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WatchlistAssetId = table.Column<int>(type: "int", nullable: false),
                    PatternType = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistAssetDetectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchlistAssetDetectors_WatchlistAssets_WatchlistAssetId",
                        column: x => x.WatchlistAssetId,
                        principalSchema: "Scanner",
                        principalTable: "WatchlistAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistAssetDetectors_AssetPattern",
                schema: "Scanner",
                table: "WatchlistAssetDetectors",
                columns: new[] { "WatchlistAssetId", "PatternType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchlistAssetDetectors",
                schema: "Scanner");
        }
    }
}
