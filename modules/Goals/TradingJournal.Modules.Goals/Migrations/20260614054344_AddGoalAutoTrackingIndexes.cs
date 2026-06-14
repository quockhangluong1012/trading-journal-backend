using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Goals.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalAutoTrackingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GoalTasks_CreatedBy_MetricSource_TrackingMode_IsCompleted",
                schema: "Goals",
                table: "GoalTasks",
                columns: new[] { "CreatedBy", "MetricSource", "TrackingMode", "IsCompleted" },
                filter: "[IsCompleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CreatedBy_MetricSource_TrackingMode_IsCompleted",
                schema: "Goals",
                table: "Goals",
                columns: new[] { "CreatedBy", "MetricSource", "TrackingMode", "IsCompleted" },
                filter: "[IsCompleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GoalMilestones_CreatedBy_MetricSource_TrackingMode_IsCompleted",
                schema: "Goals",
                table: "GoalMilestones",
                columns: new[] { "CreatedBy", "MetricSource", "TrackingMode", "IsCompleted" },
                filter: "[IsCompleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoalTasks_CreatedBy_MetricSource_TrackingMode_IsCompleted",
                schema: "Goals",
                table: "GoalTasks");

            migrationBuilder.DropIndex(
                name: "IX_Goals_CreatedBy_MetricSource_TrackingMode_IsCompleted",
                schema: "Goals",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_GoalMilestones_CreatedBy_MetricSource_TrackingMode_IsCompleted",
                schema: "Goals",
                table: "GoalMilestones");
        }
    }
}
