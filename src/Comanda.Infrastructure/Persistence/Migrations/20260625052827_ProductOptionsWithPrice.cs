using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductOptionsWithPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // text[] no se puede castear a jsonb en Postgres: se eliminan las columnas
            // viejas y se crean como jsonb. Las opciones del demo las restaura el seeder.
            migrationBuilder.DropColumn(name: "Variants", table: "Products");
            migrationBuilder.DropColumn(name: "Extras", table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "Variants", table: "Products", type: "jsonb", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Extras", table: "Products", type: "jsonb", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Variants", table: "Products");
            migrationBuilder.DropColumn(name: "Extras", table: "Products");

            migrationBuilder.AddColumn<List<string>>(
                name: "Variants", table: "Products", type: "text[]", nullable: false, defaultValue: new List<string>());
            migrationBuilder.AddColumn<List<string>>(
                name: "Extras", table: "Products", type: "text[]", nullable: false, defaultValue: new List<string>());
        }
    }
}
