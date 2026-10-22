using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public sealed class CorporateMembershipConfiguration
    : IEntityTypeConfiguration<CorporateMembership>
{
    public void Configure(EntityTypeBuilder<CorporateMembership> b)
    {
        b.ToTable("corporate_memberships");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Role).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();

        b.HasOne<CorporateCompany>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CompanyId, x.UserId }).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}
