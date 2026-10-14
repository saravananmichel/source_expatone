using System;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpatOne.Infrastructure.Persistence.Migrations;

public partial class AddDocumentTextExtraction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExtractedText",
            table: "documents",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ExtractionAttempts",
            table: "documents",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "ExtractionLeaseToken",
            table: "documents",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExtractionLeaseUntil",
            table: "documents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExtractionStatus",
            table: "documents",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "NOT_REQUESTED");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExtractedText", table: "documents");
        migrationBuilder.DropColumn(name: "ExtractionAttempts", table: "documents");
        migrationBuilder.DropColumn(name: "ExtractionLeaseToken", table: "documents");
        migrationBuilder.DropColumn(name: "ExtractionLeaseUntil", table: "documents");
        migrationBuilder.DropColumn(name: "ExtractionStatus", table: "documents");
    }
}
