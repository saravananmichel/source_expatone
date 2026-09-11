using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOriginalFileNameAndSeedDocumentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "documents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "HasExpiry", "IsSystem", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Identity", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), "International travel document", true, true, "Passport", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Immigration", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), "Entry/stay permit", true, true, "Visa", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Immigration", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), "Work authorization", true, true, "Employment Pass", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Identity", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), "Driving authorization", true, true, "Driving Licence", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Insurance", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), "Insurance policy document", true, true, "Insurance", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Medical", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), "Health/medical card", true, true, "Medical Card", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "Immigration", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810), "Work authorization permit", true, true, "Work Permit", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810) },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "Government", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810), "Official government correspondence", false, true, "Government Letter", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810) },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "General", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810), "Other document type", false, true, "Other", new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "documents");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 4, 28, 24, 26, DateTimeKind.Utc).AddTicks(1530), new DateTime(2026, 9, 11, 4, 28, 24, 26, DateTimeKind.Utc).AddTicks(1530) });
        }
    }
}
