using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class EmergencyResourceConfiguration : IEntityTypeConfiguration<EmergencyResource>
{
    public void Configure(EntityTypeBuilder<EmergencyResource> builder)
    {
        builder.ToTable("emergency_resources");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(e => e.Address)
            .HasMaxLength(500);

        builder.Property(e => e.CountryCode)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(e => e.State)
            .HasMaxLength(100);

        builder.Property(e => e.City)
            .HasMaxLength(100);

        builder.HasIndex(e => e.CountryCode);
        builder.HasIndex(e => e.Category);
    }
}
