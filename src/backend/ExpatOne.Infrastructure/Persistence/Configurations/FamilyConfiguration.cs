using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class FamilyStatusConfiguration : IEntityTypeConfiguration<FamilyRequestStatus>
{
    public void Configure(EntityTypeBuilder<FamilyRequestStatus> b)
    {
        b.ToTable("family_request_statuses"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasData(new FamilyRequestStatus { Id=1, Name="Pending" },
            new FamilyRequestStatus { Id=2, Name="Accepted" },
            new FamilyRequestStatus { Id=3, Name="Rejected" },
            new FamilyRequestStatus { Id=4, Name="Cancelled" },
            new FamilyRequestStatus { Id=5, Name="Revoked" });
    }
}
public class FamilyRequestConfiguration : IEntityTypeConfiguration<FamilyRequest>
{
    public void Configure(EntityTypeBuilder<FamilyRequest> b)
    {
        b.ToTable("family_requests", t => t.HasCheckConstraint("CK_family_not_self", "\"RequesterUserId\" <> \"RecipientUserId\""));
        b.HasKey(x => x.Id);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RequesterUserId, x.RecipientUserId }).IsUnique()
            .HasFilter("\"StatusId\" IN (1, 2)");
        b.HasIndex(x => new { x.RecipientUserId, x.StatusId });
    }
}
public class FamilyGrantConfiguration : IEntityTypeConfiguration<FamilyDocumentGrant>
{
    public void Configure(EntityTypeBuilder<FamilyDocumentGrant> b)
    {
        b.ToTable("family_document_grants"); b.HasKey(x => new { x.FamilyRequestId, x.DocumentId });
        b.HasOne(x => x.Request).WithMany(x => x.Grants).HasForeignKey(x => x.FamilyRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
