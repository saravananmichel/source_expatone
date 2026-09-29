using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVersioningSharingAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetShareId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_audit_logs_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_shares_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_shares_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_shares_users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    S3ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_versions_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_versions_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(4070), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(4070) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000013"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000015"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520) });

            migrationBuilder.CreateIndex(
                name: "IX_document_audit_logs_CreatedAt",
                table: "document_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_document_audit_logs_DocumentId",
                table: "document_audit_logs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_audit_logs_UserId",
                table: "document_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_shares_DocumentId_SharedWithUserId_RevokedAt",
                table: "document_shares",
                columns: new[] { "DocumentId", "SharedWithUserId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_shares_OwnerUserId",
                table: "document_shares",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_shares_SharedWithUserId",
                table: "document_shares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_DocumentId_IsCurrent",
                table: "document_versions",
                columns: new[] { "DocumentId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_DocumentId_VersionNumber",
                table: "document_versions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_UploadedByUserId",
                table: "document_versions",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_audit_logs");

            migrationBuilder.DropTable(
                name: "document_shares");

            migrationBuilder.DropTable(
                name: "document_versions");

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

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000012"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000013"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000015"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090), new DateTime(2026, 9, 17, 16, 9, 59, 920, DateTimeKind.Utc).AddTicks(5090) });
        }
    }
}
