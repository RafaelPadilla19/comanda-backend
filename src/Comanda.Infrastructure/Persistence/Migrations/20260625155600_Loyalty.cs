using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Loyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyEarnRate",
                table: "Tenants",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "LoyaltyEnabled",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyRedeemRate",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsEarned",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsRedeemed",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoyaltyEarnRate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyRedeemRate",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PointsEarned",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PointsRedeemed",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Customers");
        }
    }
}
