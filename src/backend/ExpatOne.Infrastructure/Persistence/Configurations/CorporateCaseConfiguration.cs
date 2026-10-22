using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public sealed class CorporateCaseConfiguration
    : IEntityTypeConfiguration<CorporateCase>
{
    public void Configure(EntityTypeBuilder<CorporateCase> b)
    {
        b.ToTable("corporate_cases");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Title).HasMaxLength(180).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasOne<CorporateCompany>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<VisaApplication>()
            .WithMany()
            .HasForeignKey(x => x.VisaApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CompanyId, x.EmployeeUserId });
        b.HasIndex(x => x.EmployeeUserId);
        b.HasIndex(x => x.VisaApplicationId);
    }
}
