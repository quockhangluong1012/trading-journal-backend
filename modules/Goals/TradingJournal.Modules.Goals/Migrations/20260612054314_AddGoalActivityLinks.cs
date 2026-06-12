using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Goals.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalActivityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MetricSource",
                schema: "Goals",
                table: "GoalTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetricSource",
                schema: "Goals",
                table: "Goals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetricSource",
                schema: "Goals",
                table: "GoalMilestones",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoalActivityLinks",
                schema: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    MetricSource = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedItem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalActivityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalActivityLinks_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "Goals",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalActivityLinks_GoalId_RecordedAt",
                schema: "Goals",
                table: "GoalActivityLinks",
                columns: new[] { "GoalId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalActivityLinks_SourceEventId_ItemType_ItemId",
                schema: "Goals",
                table: "GoalActivityLinks",
                columns: new[] { "SourceEventId", "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoalActivityLinks_SourceType_SourceId",
                schema: "Goals",
                table: "GoalActivityLinks",
                columns: new[] { "SourceType", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalActivityLinks",
                schema: "Goals");

            migrationBuilder.DropColumn(
                name: "MetricSource",
                schema: "Goals",
                table: "GoalTasks");

            migrationBuilder.DropColumn(
                name: "MetricSource",
                schema: "Goals",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "MetricSource",
                schema: "Goals",
                table: "GoalMilestones");
        }
    }
}
