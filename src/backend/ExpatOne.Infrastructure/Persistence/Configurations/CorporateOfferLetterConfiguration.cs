using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public sealed class CorporateOfferLetterConfiguration : IEntityTypeConfiguration<CorporateOfferLetter>
{
    public void Configure(EntityTypeBuilder<CorporateOfferLetter> b)
    {
        b.ToTable("corporate_offer_letters");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.TemplateDataJson).HasColumnType("text").IsRequired();
        b.Property(x => x.Response).HasMaxLength(24);
        b.HasOne<CorporateCompany>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CompanyId, x.EmployeeUserId, x.CreatedAt });
        b.HasIndex(x => x.ReplacesOfferId).IsUnique();
        b.HasOne<CorporateOfferLetter>().WithMany().HasForeignKey(x => x.ReplacesOfferId).OnDelete(DeleteBehavior.Restrict);
    }
}
