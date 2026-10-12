using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ExpatOne.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ExpatOneDbContext))]
[Migration("20261012000000_AddTemporaryFamilySharing")]
public partial class AddTemporaryFamilySharing : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<DateTime>(
        name: "AccessExpiresAt", table: "family_requests", type: "timestamp with time zone", nullable: true);
    protected override void Down(MigrationBuilder m) => m.DropColumn("AccessExpiresAt", "family_requests");
}
