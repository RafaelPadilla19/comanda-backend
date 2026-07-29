using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BranchCoverageRadius : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CustomerLat",
                table: "Orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CustomerLng",
                table: "Orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryDistanceKm",
                table: "Orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CoverageRadiusKm",
                table: "Branches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryBaseFee",
                table: "Branches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFeePerKm",
                table: "Branches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerLat",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerLng",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CoverageRadiusKm",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "DeliveryBaseFee",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "DeliveryFeePerKm",
                table: "Branches");
        }
    }
}
