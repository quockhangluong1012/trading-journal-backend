using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Goals.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalFirstCompletedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstCompletedDate",
                schema: "Goals",
                table: "GoalTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstCompletedDate",
                schema: "Goals",
                table: "Goals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstCompletedDate",
                schema: "Goals",
                table: "GoalMilestones",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstCompletedDate",
                schema: "Goals",
                table: "GoalTasks");

            migrationBuilder.DropColumn(
                name: "FirstCompletedDate",
                schema: "Goals",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "FirstCompletedDate",
                schema: "Goals",
                table: "GoalMilestones");
        }
    }
}
