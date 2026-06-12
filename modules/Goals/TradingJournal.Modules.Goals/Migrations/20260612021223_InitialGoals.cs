using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Goals.Migrations
{
    /// <inheritdoc />
    public partial class InitialGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Goals");

            migrationBuilder.CreateTable(
                name: "Goals",
                schema: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrackingMode = table.Column<int>(type: "int", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetricUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetricDirection = table.Column<int>(type: "int", nullable: true),
                    StartValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoalMilestones",
                schema: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TrackingMode = table.Column<int>(type: "int", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetricUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetricDirection = table.Column<int>(type: "int", nullable: true),
                    StartValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalMilestones_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "Goals",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalTasks",
                schema: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    MilestoneId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TrackingMode = table.Column<int>(type: "int", nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetricUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetricDirection = table.Column<int>(type: "int", nullable: true),
                    StartValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalTasks_GoalMilestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalSchema: "Goals",
                        principalTable: "GoalMilestones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoalTasks_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "Goals",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalProgressEntries",
                schema: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    MilestoneId = table.Column<int>(type: "int", nullable: true),
                    GoalTaskId = table.Column<int>(type: "int", nullable: true),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PreviousIsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CurrentIsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalProgressEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_GoalMilestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalSchema: "Goals",
                        principalTable: "GoalMilestones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_GoalTasks_GoalTaskId",
                        column: x => x.GoalTaskId,
                        principalSchema: "Goals",
                        principalTable: "GoalTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoalProgressEntries_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "Goals",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalMilestones_CreatedBy",
                schema: "Goals",
                table: "GoalMilestones",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GoalMilestones_GoalId_SortOrder",
                schema: "Goals",
                table: "GoalMilestones",
                columns: new[] { "GoalId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_GoalId_CreatedDate",
                schema: "Goals",
                table: "GoalProgressEntries",
                columns: new[] { "GoalId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_GoalTaskId",
                schema: "Goals",
                table: "GoalProgressEntries",
                column: "GoalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalProgressEntries_MilestoneId",
                schema: "Goals",
                table: "GoalProgressEntries",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CreatedBy_IsCompleted_DueDate",
                schema: "Goals",
                table: "Goals",
                columns: new[] { "CreatedBy", "IsCompleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalTasks_CreatedBy",
                schema: "Goals",
                table: "GoalTasks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GoalTasks_GoalId_MilestoneId_SortOrder",
                schema: "Goals",
                table: "GoalTasks",
                columns: new[] { "GoalId", "MilestoneId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalTasks_MilestoneId",
                schema: "Goals",
                table: "GoalTasks",
                column: "MilestoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalProgressEntries",
                schema: "Goals");

            migrationBuilder.DropTable(
                name: "GoalTasks",
                schema: "Goals");

            migrationBuilder.DropTable(
                name: "GoalMilestones",
                schema: "Goals");

            migrationBuilder.DropTable(
                name: "Goals",
                schema: "Goals");
        }
    }
}
