using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public sealed class CorporateCompanyConfiguration
    : IEntityTypeConfiguration<CorporateCompany>
{
    public void Configure(EntityTypeBuilder<CorporateCompany> b)
    {
        b.ToTable("corporate_companies");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(180).IsRequired();

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CreatedByUserId);
    }
}
