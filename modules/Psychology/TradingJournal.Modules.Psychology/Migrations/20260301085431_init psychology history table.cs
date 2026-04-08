using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Psychology.Migrations
{
    /// <inheritdoc />
    public partial class initpsychologyhistorytable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Psychology");

            migrationBuilder.CreateTable(
                name: "EmotionTags",
                schema: "Psychology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmotionType = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmotionTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PsychologyJournals",
                schema: "Psychology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallMood = table.Column<int>(type: "int", nullable: false),
                    ConfidentLevel = table.Column<int>(type: "int", nullable: false),
                    TodayTradingReview = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PsychologyJournals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PsychologyJournalEmotions",
                schema: "Psychology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PsychologyJournalId = table.Column<int>(type: "int", nullable: false),
                    EmotionTagId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PsychologyJournalEmotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PsychologyJournalEmotions_EmotionTags_EmotionTagId",
                        column: x => x.EmotionTagId,
                        principalSchema: "Psychology",
                        principalTable: "EmotionTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PsychologyJournalEmotions_PsychologyJournals_PsychologyJournalId",
                        column: x => x.PsychologyJournalId,
                        principalSchema: "Psychology",
                        principalTable: "PsychologyJournals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PsychologyJournalEmotions_EmotionTagId",
                schema: "Psychology",
                table: "PsychologyJournalEmotions",
                column: "EmotionTagId");

            migrationBuilder.CreateIndex(
                name: "IX_PsychologyJournalEmotions_PsychologyJournalId",
                schema: "Psychology",
                table: "PsychologyJournalEmotions",
                column: "PsychologyJournalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PsychologyJournalEmotions",
                schema: "Psychology");

            migrationBuilder.DropTable(
                name: "EmotionTags",
                schema: "Psychology");

            migrationBuilder.DropTable(
                name: "PsychologyJournals",
                schema: "Psychology");
        }
    }
}
