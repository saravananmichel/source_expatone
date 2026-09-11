using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeExternalIdentityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_ExternalId",
                table: "users");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 4, 28, 24, 26, DateTimeKind.Utc).AddTicks(1530), new DateTime(2026, 9, 11, 4, 28, 24, 26, DateTimeKind.Utc).AddTicks(1530) });

            migrationBuilder.CreateIndex(
                name: "IX_users_ExternalProvider_ExternalId",
                table: "users",
                columns: new[] { "ExternalProvider", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_ExternalProvider_ExternalId",
                table: "users");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 10, 9, 6, 26, 697, DateTimeKind.Utc).AddTicks(2190), new DateTime(2026, 9, 10, 9, 6, 26, 697, DateTimeKind.Utc).AddTicks(2190) });

            migrationBuilder.CreateIndex(
                name: "IX_users_ExternalId",
                table: "users",
                column: "ExternalId",
                unique: true);
        }
    }
}
