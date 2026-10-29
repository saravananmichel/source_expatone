using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ExpatOne.Infrastructure.Persistence;

namespace ExpatOne.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ExpatOneDbContext))]
[Migration("20261029000000_AddUserProfilePhotos")]
public partial class AddUserProfilePhotos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("ProfilePhotoKey", "users", type: "character varying(512)", maxLength: 512, nullable: true);
        migrationBuilder.AddColumn<string>("ProfilePhotoSource", "users", type: "character varying(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<DateTime>("ProfilePhotoUpdatedAt", "users", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("GooglePhotoReference", "users", type: "character varying(2048)", maxLength: 2048, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ProfilePhotoKey", "users");
        migrationBuilder.DropColumn("ProfilePhotoSource", "users");
        migrationBuilder.DropColumn("ProfilePhotoUpdatedAt", "users");
        migrationBuilder.DropColumn("GooglePhotoReference", "users");
    }
}
