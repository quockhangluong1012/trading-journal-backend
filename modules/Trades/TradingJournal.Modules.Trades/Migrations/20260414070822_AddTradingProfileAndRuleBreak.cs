using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Trades.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingProfileAndRuleBreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRuleBroken",
                schema: "Trades",
                table: "TradeHistories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RuleBreakReason",
                schema: "Trades",
                table: "TradeHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TradingProfiles",
                schema: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaxTradesPerDay = table.Column<int>(type: "int", nullable: true),
                    MaxDailyLossPercentage = table.Column<double>(type: "float", nullable: true),
                    MaxConsecutiveLosses = table.Column<int>(type: "int", nullable: true),
                    IsDisciplineEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingProfiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingProfiles",
                schema: "Trades");

            migrationBuilder.DropColumn(
                name: "IsRuleBroken",
                schema: "Trades",
                table: "TradeHistories");

            migrationBuilder.DropColumn(
                name: "RuleBreakReason",
                schema: "Trades",
                table: "TradeHistories");
        }
    }
}
