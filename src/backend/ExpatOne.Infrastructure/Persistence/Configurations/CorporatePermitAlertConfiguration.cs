using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public sealed class CorporatePermitAlertConfiguration : IEntityTypeConfiguration<CorporatePermitAlert>
{
    public void Configure(EntityTypeBuilder<CorporatePermitAlert> b)
    {
        b.ToTable("corporate_permit_alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ExpiryDate).HasColumnType("date");
        b.HasOne<CorporateCompany>().WithMany().HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CorporateIssuedPermit>().WithMany().HasForeignKey(x => x.PermitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AcknowledgedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PermitId, x.ThresholdDays, x.ExpiryDate }).IsUnique();
        b.HasIndex(x => new { x.CompanyId, x.AcknowledgedAt });
    }
}
