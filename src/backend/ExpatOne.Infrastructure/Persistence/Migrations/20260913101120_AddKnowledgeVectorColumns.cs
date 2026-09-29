using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeVectorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "government_sources",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastIngestedAt",
                table: "government_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkIndex",
                table: "government_knowledge",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "government_knowledge",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmbeddedAt",
                table: "government_knowledge",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "government_knowledge",
                type: "vector(768)",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_government_knowledge_Embedding",
                table: "government_knowledge",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_government_knowledge_Embedding",
                table: "government_knowledge");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "government_sources");

            migrationBuilder.DropColumn(
                name: "LastIngestedAt",
                table: "government_sources");

            migrationBuilder.DropColumn(
                name: "ChunkIndex",
                table: "government_knowledge");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "government_knowledge");

            migrationBuilder.DropColumn(
                name: "EmbeddedAt",
                table: "government_knowledge");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "government_knowledge");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

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
        }
    }
}
