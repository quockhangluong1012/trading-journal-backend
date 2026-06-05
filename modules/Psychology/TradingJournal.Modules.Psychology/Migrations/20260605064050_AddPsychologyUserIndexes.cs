using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Psychology.Migrations
{
    /// <inheritdoc />
    public partial class AddPsychologyUserIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TiltSnapshots_CreatedBy_RecordedAt",
                schema: "Psychology",
                table: "TiltSnapshots",
                columns: new[] { "CreatedBy", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StreakRecords_CreatedBy_RecordedAt",
                schema: "Psychology",
                table: "StreakRecords",
                columns: new[] { "CreatedBy", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PsychologyJournals_CreatedBy_Date",
                schema: "Psychology",
                table: "PsychologyJournals",
                columns: new[] { "CreatedBy", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_KarmaRecords_CreatedBy_RecordedAt",
                schema: "Psychology",
                table: "KarmaRecords",
                columns: new[] { "CreatedBy", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyNotes_CreatedBy_NoteDate",
                schema: "Psychology",
                table: "DailyNotes",
                columns: new[] { "CreatedBy", "NoteDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_CreatedBy_UnlockedAt",
                schema: "Psychology",
                table: "Achievements",
                columns: new[] { "CreatedBy", "UnlockedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TiltSnapshots_CreatedBy_RecordedAt",
                schema: "Psychology",
                table: "TiltSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_StreakRecords_CreatedBy_RecordedAt",
                schema: "Psychology",
                table: "StreakRecords");

            migrationBuilder.DropIndex(
                name: "IX_PsychologyJournals_CreatedBy_Date",
                schema: "Psychology",
                table: "PsychologyJournals");

            migrationBuilder.DropIndex(
                name: "IX_KarmaRecords_CreatedBy_RecordedAt",
                schema: "Psychology",
                table: "KarmaRecords");

            migrationBuilder.DropIndex(
                name: "IX_DailyNotes_CreatedBy_NoteDate",
                schema: "Psychology",
                table: "DailyNotes");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_CreatedBy_UnlockedAt",
                schema: "Psychology",
                table: "Achievements");
        }
    }
}
