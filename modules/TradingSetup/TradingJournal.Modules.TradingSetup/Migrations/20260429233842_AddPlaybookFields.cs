using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Modules.Setups.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybookFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntryRules",
                schema: "Setups",
                table: "TradingSetups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitRules",
                schema: "Setups",
                table: "TradingSetups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdealMarketConditions",
                schema: "Setups",
                table: "TradingSetups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredAssets",
                schema: "Setups",
                table: "TradingSetups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredTimeframes",
                schema: "Setups",
                table: "TradingSetups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredDate",
                schema: "Setups",
                table: "TradingSetups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetiredReason",
                schema: "Setups",
                table: "TradingSetups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskPerTrade",
                schema: "Setups",
                table: "TradingSetups",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetRiskReward",
                schema: "Setups",
                table: "TradingSetups",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryRules",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "ExitRules",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "IdealMarketConditions",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "PreferredAssets",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "PreferredTimeframes",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "RetiredDate",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "RetiredReason",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "RiskPerTrade",
                schema: "Setups",
                table: "TradingSetups");

            migrationBuilder.DropColumn(
                name: "TargetRiskReward",
                schema: "Setups",
                table: "TradingSetups");
        }
    }
}
