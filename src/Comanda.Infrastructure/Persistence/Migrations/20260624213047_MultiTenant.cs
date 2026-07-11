using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Printers_Key",
                table: "Printers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_Key",
                table: "PaymentMethods");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Code",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Printers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PaymentMethods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OrderItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "InventoryItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Drivers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeliveryZones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CashSessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CashMovements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Branches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId",
                table: "Products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TenantId",
                table: "Printers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_TenantId_Key",
                table: "Printers",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_TenantId",
                table: "PaymentMethods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_TenantId_Key",
                table: "PaymentMethods",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId",
                table: "Orders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_Code",
                table: "Orders",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_TenantId",
                table: "OrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId",
                table: "InventoryItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId",
                table: "Drivers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId",
                table: "DeliveryZones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Phone",
                table: "Customers",
                columns: new[] { "TenantId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId",
                table: "Categories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_Name",
                table: "Categories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_TenantId",
                table: "CashSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TenantId",
                table: "CashMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId",
                table: "Branches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Printers_TenantId",
                table: "Printers");

            migrationBuilder.DropIndex(
                name: "IX_Printers_TenantId_Key",
                table: "Printers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_TenantId",
                table: "PaymentMethods");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_TenantId_Key",
                table: "PaymentMethods");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_Code",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_TenantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_TenantId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_TenantId",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_TenantId",
                table: "DeliveryZones");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_Phone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_CashSessions_TenantId",
                table: "CashSessions");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_TenantId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_Branches_TenantId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CashSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Branches");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_Key",
                table: "Printers",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Key",
                table: "PaymentMethods",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Code",
                table: "Orders",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
