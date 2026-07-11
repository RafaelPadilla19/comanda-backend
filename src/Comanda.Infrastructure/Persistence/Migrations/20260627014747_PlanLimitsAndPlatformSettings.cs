using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanLimitsAndPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdvancedReports",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Coupons",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Inventory",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Loyalty",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxOrdersMonth",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "OnlinePayments",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OveragePrice",
                table: "Plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RetentionMonths",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WompiAppId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WompiApiSecretEnc = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "AdvancedReports",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Coupons",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Inventory",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Loyalty",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxOrdersMonth",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "OnlinePayments",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "OveragePrice",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "RetentionMonths",
                table: "Plans");
        }
    }
}
