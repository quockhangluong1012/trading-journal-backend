using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Psychology.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyNotesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyNotes",
                schema: "Psychology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoteDate = table.Column<DateTime>(type: "date", nullable: false),
                    DailyBias = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MarketStructureNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyLevelsAndLiquidity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewsAndEvents = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionFocus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RiskAppetite = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MentalState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyRulesAndReminders = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyNotes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyNotes",
                schema: "Psychology");
        }
    }
}
