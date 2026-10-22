using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateMobility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "corporate_companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_corporate_companies_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "corporate_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisaApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_corporate_cases_corporate_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "corporate_companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_corporate_cases_users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_corporate_cases_visa_applications_VisaApplicationId",
                        column: x => x.VisaApplicationId,
                        principalTable: "visa_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "corporate_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_corporate_memberships_corporate_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "corporate_companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_corporate_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_cases_CompanyId_EmployeeUserId",
                table: "corporate_cases",
                columns: new[] { "CompanyId", "EmployeeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_cases_EmployeeUserId",
                table: "corporate_cases",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_corporate_cases_VisaApplicationId",
                table: "corporate_cases",
                column: "VisaApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_corporate_companies_CreatedByUserId",
                table: "corporate_companies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_corporate_memberships_CompanyId_UserId",
                table: "corporate_memberships",
                columns: new[] { "CompanyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_corporate_memberships_UserId",
                table: "corporate_memberships",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corporate_cases");

            migrationBuilder.DropTable(
                name: "corporate_memberships");

            migrationBuilder.DropTable(
                name: "corporate_companies");
        }
    }
}
