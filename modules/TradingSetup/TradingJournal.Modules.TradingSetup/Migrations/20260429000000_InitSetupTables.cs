using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Setups.Migrations
{
    /// <inheritdoc />
    public partial class InitSetupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Setups");

            migrationBuilder.CreateTable(
                name: "TradingSetups",
                schema: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSetups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetupSteps",
                schema: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingSetupId = table.Column<int>(type: "int", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NodeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PositionX = table.Column<double>(type: "float", nullable: false),
                    PositionY = table.Column<double>(type: "float", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupSteps_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalSchema: "Setups",
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetupConnections",
                schema: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingSetupId = table.Column<int>(type: "int", nullable: false),
                    SourceStepId = table.Column<int>(type: "int", nullable: false),
                    TargetStepId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAnimated = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupConnections_SetupSteps_SourceStepId",
                        column: x => x.SourceStepId,
                        principalSchema: "Setups",
                        principalTable: "SetupSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupConnections_SetupSteps_TargetStepId",
                        column: x => x.TargetStepId,
                        principalSchema: "Setups",
                        principalTable: "SetupSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupConnections_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalSchema: "Setups",
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupConnections_SourceStepId",
                schema: "Setups",
                table: "SetupConnections",
                column: "SourceStepId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupConnections_TargetStepId",
                schema: "Setups",
                table: "SetupConnections",
                column: "TargetStepId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupConnections_TradingSetupId",
                schema: "Setups",
                table: "SetupConnections",
                column: "TradingSetupId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupSteps_TradingSetupId",
                schema: "Setups",
                table: "SetupSteps",
                column: "TradingSetupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupConnections",
                schema: "Setups");

            migrationBuilder.DropTable(
                name: "SetupSteps",
                schema: "Setups");

            migrationBuilder.DropTable(
                name: "TradingSetups",
                schema: "Setups");
        }
    }
}
