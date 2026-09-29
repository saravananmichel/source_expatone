using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAndOnboardingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmploymentStatus",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyStatus",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasChildren",
                table: "users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "users",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfChildren",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnboardingCompleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResidenceLocation",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisaPassType",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FamilyStatus",
                table: "users");

            migrationBuilder.DropColumn(
                name: "HasChildren",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "users");

            migrationBuilder.DropColumn(
                name: "NumberOfChildren",
                table: "users");

            migrationBuilder.DropColumn(
                name: "OnboardingCompleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ResidenceLocation",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VisaPassType",
                table: "users");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6910), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6910) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6910), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6910) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6910), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6910) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920), new DateTime(2026, 9, 13, 10, 11, 20, 705, DateTimeKind.Utc).AddTicks(6920) });
        }
    }
}
