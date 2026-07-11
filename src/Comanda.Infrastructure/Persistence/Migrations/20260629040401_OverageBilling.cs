using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OverageBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OverageBilledOrders",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OverageCycleKey",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OverageAmount",
                table: "SubscriptionPayments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OverageCycleKey",
                table: "SubscriptionPayments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OverageOrders",
                table: "SubscriptionPayments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverageBilledOrders",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OverageCycleKey",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OverageAmount",
                table: "SubscriptionPayments");

            migrationBuilder.DropColumn(
                name: "OverageCycleKey",
                table: "SubscriptionPayments");

            migrationBuilder.DropColumn(
                name: "OverageOrders",
                table: "SubscriptionPayments");
        }
    }
}
