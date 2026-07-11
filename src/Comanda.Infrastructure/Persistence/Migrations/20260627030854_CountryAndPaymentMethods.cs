using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountryAndPaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Tenants",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CountryPaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryPaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CountryPaymentMethods_Country_Key",
                table: "CountryPaymentMethods",
                columns: new[] { "Country", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountryPaymentMethods");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Tenants");
        }
    }
}
