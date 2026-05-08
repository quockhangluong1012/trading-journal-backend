using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.AiInsights.Migrations
{
    /// <inheritdoc />
    public partial class AddMorningBriefings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Trades");

            migrationBuilder.CreateTable(
                name: "MorningBriefings",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BriefingDateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    Greeting = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Briefing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionItem = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OverallMood = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FocusAreasJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MorningBriefings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MorningBriefings_CreatedBy_BriefingDateUtc",
                schema: "Trades",
                table: "MorningBriefings",
                columns: new[] { "CreatedBy", "BriefingDateUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MorningBriefings",
                schema: "Trades");
        }
    }
}
