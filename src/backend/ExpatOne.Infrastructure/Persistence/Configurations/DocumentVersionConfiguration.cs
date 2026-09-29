using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.S3ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.OriginalFileName)
            .HasMaxLength(300);

        builder.Property(v => v.ContentType)
            .HasMaxLength(100);

        builder.Property(v => v.Sha256Hash)
            .HasMaxLength(64);

        builder.HasOne(v => v.Document)
            .WithMany(d => d.Versions)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.UploadedByUser)
            .WithMany()
            .HasForeignKey(v => v.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
        builder.HasIndex(v => new { v.DocumentId, v.IsCurrent });
    }
}
