using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class GovernmentSourceConfiguration : IEntityTypeConfiguration<GovernmentSource>
{
    public void Configure(EntityTypeBuilder<GovernmentSource> builder)
    {
        builder.ToTable("government_sources");

        builder.HasKey(gs => gs.Id);

        builder.Property(gs => gs.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(gs => gs.Url)
            .HasMaxLength(500);

        builder.Property(gs => gs.Department)
            .HasMaxLength(200);

        builder.Property(gs => gs.CountryCode)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(gs => gs.ContentHash)
            .HasMaxLength(64);

        builder.HasIndex(gs => gs.CountryCode);
    }
}
