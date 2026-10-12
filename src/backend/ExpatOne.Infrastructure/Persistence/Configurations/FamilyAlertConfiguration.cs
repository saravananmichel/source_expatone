using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class FamilyExpiryConfirmationConfiguration : IEntityTypeConfiguration<FamilyExpiryConfirmation>
{
    public void Configure(EntityTypeBuilder<FamilyExpiryConfirmation> b)
    {
        b.ToTable("family_expiry_confirmations");
        b.HasKey(x => x.DocumentId);
        b.Property(x => x.DocumentId).ValueGeneratedNever();
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired().IsConcurrencyToken();
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class FamilyAlertDismissalConfiguration : IEntityTypeConfiguration<FamilyAlertDismissal>
{
    public void Configure(EntityTypeBuilder<FamilyAlertDismissal> b)
    {
        b.ToTable("family_alert_dismissals");
        b.HasKey(x => new { x.UserId, x.AlertId });
        b.Property(x => x.AlertId).HasMaxLength(64).IsRequired();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
