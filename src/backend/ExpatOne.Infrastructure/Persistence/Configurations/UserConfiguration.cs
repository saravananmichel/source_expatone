using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.ExternalId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.ExternalProvider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(200);

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.CountryCode)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(u => u.PreferredLanguage)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(u => u.ExternalId)
            .IsUnique();

        builder.HasIndex(u => u.Email)
            .IsUnique();
    }
}
