using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCurrencyTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Tenants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Tenants",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "$");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Tenants",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.13m);

            // Tenants existentes: precargar moneda/IVA según su país ya guardado, en vez de
            // dejarlos todos en el default USD/13% (la mayoría ya es El Salvador, pero evita
            // suponerlo para los que no lo son).
            migrationBuilder.Sql(@"
                UPDATE ""Tenants"" SET ""Currency"" = 'GTQ', ""CurrencySymbol"" = 'Q', ""TaxRate"" = 0.12 WHERE ""Country"" = 'GT';
                UPDATE ""Tenants"" SET ""Currency"" = 'HNL', ""CurrencySymbol"" = 'L', ""TaxRate"" = 0.15 WHERE ""Country"" = 'HN';
                UPDATE ""Tenants"" SET ""Currency"" = 'NIO', ""CurrencySymbol"" = 'C$', ""TaxRate"" = 0.15 WHERE ""Country"" = 'NI';
                UPDATE ""Tenants"" SET ""Currency"" = 'CRC', ""CurrencySymbol"" = '₡', ""TaxRate"" = 0.13 WHERE ""Country"" = 'CR';
                UPDATE ""Tenants"" SET ""Currency"" = 'USD', ""CurrencySymbol"" = '$', ""TaxRate"" = 0.07 WHERE ""Country"" = 'PA';
                UPDATE ""Tenants"" SET ""Currency"" = 'COP', ""CurrencySymbol"" = '$', ""TaxRate"" = 0.19 WHERE ""Country"" = 'CO';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CurrencySymbol",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Tenants");
        }
    }
}
