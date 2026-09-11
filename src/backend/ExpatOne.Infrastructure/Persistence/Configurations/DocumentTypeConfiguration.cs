using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("document_types");

        builder.HasKey(dt => dt.Id);

        builder.Property(dt => dt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(dt => dt.Description)
            .HasMaxLength(500);

        builder.Property(dt => dt.Category)
            .HasMaxLength(50);

        builder.HasIndex(dt => dt.Name)
            .IsUnique();
    }
}
