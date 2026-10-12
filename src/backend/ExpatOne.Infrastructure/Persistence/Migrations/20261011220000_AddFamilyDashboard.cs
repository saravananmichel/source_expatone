using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ExpatOne.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ExpatOneDbContext))]
[Migration("20261011220000_AddFamilyDashboard")]
public partial class AddFamilyDashboard : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>(name: "AllowExpiryReminders", table: "family_requests", type: "boolean", nullable: false, defaultValue: false);
        m.CreateTable("family_expiry_confirmations", columns: t => new {
            DocumentId = t.Column<Guid>(type: "uuid", nullable: false),
            Fingerprint = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ConfirmedAt = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_family_expiry_confirmations", x => x.DocumentId);
            t.ForeignKey("FK_family_expiry_confirmations_documents_DocumentId", x => x.DocumentId,
                "documents", "Id", onDelete: ReferentialAction.Cascade);
        });
        m.CreateTable("family_alert_dismissals", columns: t => new {
            UserId = t.Column<Guid>(type: "uuid", nullable: false),
            AlertId = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            DismissedAt = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_family_alert_dismissals", x => new { x.UserId, x.AlertId });
            t.ForeignKey("FK_family_alert_dismissals_users_UserId", x => x.UserId,
                "users", "Id", onDelete: ReferentialAction.Cascade);
        });
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("family_alert_dismissals");
        m.DropTable("family_expiry_confirmations");
        m.DropColumn("AllowExpiryReminders", "family_requests");
    }
}
