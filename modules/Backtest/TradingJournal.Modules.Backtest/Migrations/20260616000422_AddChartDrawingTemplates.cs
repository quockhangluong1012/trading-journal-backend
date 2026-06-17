using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Backtest.Migrations
{
    /// <inheritdoc />
    public partial class AddChartDrawingTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartDrawingTemplates",
                schema: "Backtest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StyleJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tool = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartDrawingTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartDrawingTemplates_UserActiveCreated",
                schema: "Backtest",
                table: "ChartDrawingTemplates",
                columns: new[] { "CreatedBy", "IsDisabled", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartDrawingTemplates",
                schema: "Backtest");
        }
    }
}
