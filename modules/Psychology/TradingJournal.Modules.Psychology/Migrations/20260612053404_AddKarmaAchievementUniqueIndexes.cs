using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Psychology.Migrations
{
    /// <inheritdoc />
    public partial class AddKarmaAchievementUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Achievements_CreatedBy_UnlockedAt",
                schema: "Psychology",
                table: "Achievements");

            migrationBuilder.CreateIndex(
                name: "IX_KarmaRecords_CreatedBy_ActionType_ReferenceId",
                schema: "Psychology",
                table: "KarmaRecords",
                columns: new[] { "CreatedBy", "ActionType", "ReferenceId" },
                unique: true,
                filter: "[ReferenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_CreatedBy_AchievementType",
                schema: "Psychology",
                table: "Achievements",
                columns: new[] { "CreatedBy", "AchievementType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KarmaRecords_CreatedBy_ActionType_ReferenceId",
                schema: "Psychology",
                table: "KarmaRecords");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_CreatedBy_AchievementType",
                schema: "Psychology",
                table: "Achievements");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_CreatedBy_UnlockedAt",
                schema: "Psychology",
                table: "Achievements",
                columns: new[] { "CreatedBy", "UnlockedAt" });
        }
    }
}
