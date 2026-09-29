using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderDaysBeforeExpiryAndCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reminders_documents_DocumentId",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "IX_reminders_DocumentId",
                table: "reminders");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                table: "reminders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DaysBeforeExpiry",
                table: "reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(750), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(750) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2640), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2640) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2660), new DateTime(2026, 9, 11, 5, 58, 8, 742, DateTimeKind.Utc).AddTicks(2660) });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_DocumentId_DaysBeforeExpiry",
                table: "reminders",
                columns: new[] { "DocumentId", "DaysBeforeExpiry" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_reminders_documents_DocumentId",
                table: "reminders",
                column: "DocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reminders_documents_DocumentId",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "IX_reminders_DocumentId_DaysBeforeExpiry",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "DaysBeforeExpiry",
                table: "reminders");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                table: "reminders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1800) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810), new DateTime(2026, 9, 11, 5, 22, 13, 858, DateTimeKind.Utc).AddTicks(1810) });

            migrationBuilder.CreateIndex(
                name: "IX_reminders_DocumentId",
                table: "reminders",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_reminders_documents_DocumentId",
                table: "reminders",
                column: "DocumentId",
                principalTable: "documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
