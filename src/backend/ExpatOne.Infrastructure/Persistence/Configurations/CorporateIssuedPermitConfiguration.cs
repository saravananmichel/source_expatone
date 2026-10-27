using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public sealed class CorporateIssuedPermitConfiguration : IEntityTypeConfiguration<CorporateIssuedPermit>
{
    public void Configure(EntityTypeBuilder<CorporateIssuedPermit> b)
    {
        b.ToTable("corporate_issued_permits");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.CountryCode).HasMaxLength(8).IsRequired();
        b.Property(x => x.PermitType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(120);
        b.Property(x => x.IssuedOn).HasColumnType("date");
        b.Property(x => x.ExpiresOn).HasColumnType("date");
        b.HasOne<CorporateCompany>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CompanyId, x.EmployeeUserId, x.ExpiresOn });
    }
}
