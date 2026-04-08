using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class updatechecklistschema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the ChecklistModels table first so a default row can be inserted.
            migrationBuilder.CreateTable(
                name: "ChecklistModels",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistModels", x => x.Id);
                });

            // 2. Seed a default model so existing PretradeChecklists rows can reference it.
            migrationBuilder.Sql(
                "SET IDENTITY_INSERT [Trades].[ChecklistModels] ON; " +
                "INSERT INTO [Trades].[ChecklistModels] ([Id],[Name],[Description],[CreatedDate],[CreatedBy],[IsDisabled],[UpdatedDate],[UpdatedBy]) " +
                "VALUES (1, N'Uncategorized', NULL, GETUTCDATE(), 0, 0, NULL, NULL); " +
                "SET IDENTITY_INSERT [Trades].[ChecklistModels] OFF;");

            // 3. Add ChecklistModelId as nullable first to avoid immediate constraint violations.
            migrationBuilder.AddColumn<int>(
                name: "ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists",
                type: "int",
                nullable: true);

            // 4. Point all existing rows to the default model.
            migrationBuilder.Sql(
                "UPDATE [Trades].[PretradeChecklists] SET [ChecklistModelId] = 1 WHERE [ChecklistModelId] IS NULL;");

            // 5. Make the column non-nullable now that every row has a valid FK value.
            migrationBuilder.AlterColumn<int>(
                name: "ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 6. Index + FK constraint.
            migrationBuilder.CreateIndex(
                name: "IX_PretradeChecklists_ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists",
                column: "ChecklistModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_PretradeChecklists_ChecklistModels_ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists",
                column: "ChecklistModelId",
                principalSchema: "Trades",
                principalTable: "ChecklistModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PretradeChecklists_ChecklistModels_ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists");

            migrationBuilder.DropTable(
                name: "ChecklistModels",
                schema: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_PretradeChecklists_ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists");

            migrationBuilder.DropColumn(
                name: "ChecklistModelId",
                schema: "Trades",
                table: "PretradeChecklists");
        }
    }
}
