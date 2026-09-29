using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIntelligenceTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(3200), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(3200) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "HasExpiry", "IsSystem", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000010"), "Immigration", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), "Immigration-related document", true, true, "Immigration Document", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) },
                    { new Guid("10000000-0000-0000-0000-000000000011"), "Employment", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), "Employment agreement or offer letter", false, true, "Employment Contract", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) },
                    { new Guid("10000000-0000-0000-0000-000000000012"), "Housing", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), "Tenancy or rental agreement", true, true, "Rental Agreement", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) },
                    { new Guid("10000000-0000-0000-0000-000000000013"), "Financial", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), "Tax assessment, return, or receipt", false, true, "Tax Document", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) },
                    { new Guid("10000000-0000-0000-0000-000000000014"), "Government", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), "General government correspondence", false, true, "Government Correspondence", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) },
                    { new Guid("10000000-0000-0000-0000-000000000015"), "General", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), "Non-government correspondence or letter", false, true, "General Correspondence", new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000015"));

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 748, DateTimeKind.Utc).AddTicks(9320), new DateTime(2026, 9, 17, 15, 44, 28, 748, DateTimeKind.Utc).AddTicks(9320) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1430), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1450), new DateTime(2026, 9, 17, 15, 44, 28, 749, DateTimeKind.Utc).AddTicks(1450) });
        }
    }
}
