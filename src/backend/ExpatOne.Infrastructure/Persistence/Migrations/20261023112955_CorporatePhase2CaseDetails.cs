using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorporatePhase2CaseDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedHrUserId",
                table: "corporate_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "corporate_cases",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "corporate_cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "corporate_cases",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "corporate_member_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OldValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_member_audit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_member_audit_CompanyId_MembershipId_CreatedAt",
                table: "corporate_member_audit",
                columns: new[] { "CompanyId", "MembershipId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corporate_member_audit");

            migrationBuilder.DropColumn(
                name: "AssignedHrUserId",
                table: "corporate_cases");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "corporate_cases");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "corporate_cases");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "corporate_cases");
        }
    }
}
