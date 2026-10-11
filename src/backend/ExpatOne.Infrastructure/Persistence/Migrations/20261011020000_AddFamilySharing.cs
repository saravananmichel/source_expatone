using System;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ExpatOne.Infrastructure.Persistence.Migrations;
[DbContext(typeof(ExpatOneDbContext))]
[Migration("20261011020000_AddFamilySharing")]
public partial class AddFamilySharing : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("family_request_statuses", columns: t => new {
            Id = t.Column<int>(nullable: false), Name = t.Column<string>(maxLength: 30, nullable: false)
        }, constraints: t => t.PrimaryKey("PK_family_request_statuses", x => x.Id));
        m.CreateTable("family_requests", columns: t => new {
            Id = t.Column<Guid>(nullable: false), RequesterUserId = t.Column<Guid>(nullable: false),
            RecipientUserId = t.Column<Guid>(nullable: false), StatusId = t.Column<int>(nullable: false),
            Revision = t.Column<Guid>(nullable: false), CreatedAt = t.Column<DateTime>(nullable: false),
            UpdatedAt = t.Column<DateTime>(nullable: false), RespondedAt = t.Column<DateTime>(nullable: true)
        }, constraints: t => {
            t.PrimaryKey("PK_family_requests", x => x.Id);
            t.CheckConstraint("CK_family_not_self", "\"RequesterUserId\" <> \"RecipientUserId\"");
            t.ForeignKey("FK_family_requests_users_RequesterUserId", x => x.RequesterUserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_family_requests_users_RecipientUserId", x => x.RecipientUserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_family_requests_family_request_statuses_StatusId", x => x.StatusId, "family_request_statuses", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateTable("family_document_grants", columns: t => new {
            FamilyRequestId = t.Column<Guid>(nullable: false), DocumentId = t.Column<Guid>(nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_family_document_grants", x => new { x.FamilyRequestId, x.DocumentId });
            t.ForeignKey("FK_family_document_grants_family_requests_FamilyRequestId", x => x.FamilyRequestId, "family_requests", "Id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_family_document_grants_documents_DocumentId", x => x.DocumentId, "documents", "Id", onDelete: ReferentialAction.Cascade);
        });
        m.CreateIndex("IX_family_request_statuses_Name", "family_request_statuses", "Name", unique: true);
        m.CreateIndex("IX_family_requests_RequesterUserId_RecipientUserId", "family_requests", new[] { "RequesterUserId", "RecipientUserId" }, unique: true, filter: "\"StatusId\" IN (1, 2)");
        m.CreateIndex("IX_family_requests_RecipientUserId_StatusId", "family_requests", new[] { "RecipientUserId", "StatusId" });
        m.CreateIndex("IX_family_requests_StatusId", "family_requests", "StatusId");
        m.CreateIndex("IX_family_document_grants_DocumentId", "family_document_grants", "DocumentId");
        m.InsertData("family_request_statuses", new[] { "Id", "Name" }, new object[,] {
            {1, "Pending"}, {2, "Accepted"}, {3, "Rejected"}, {4, "Cancelled"}, {5, "Revoked"}
        });
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("family_document_grants"); m.DropTable("family_requests"); m.DropTable("family_request_statuses");
    }
}
