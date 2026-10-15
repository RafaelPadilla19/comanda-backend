using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comanda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OtpHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OtpExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ResetToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResetTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_Email",
                table: "PasswordResetRequests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ResetToken",
                table: "PasswordResetRequests",
                column: "ResetToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetRequests");
        }
    }
}
