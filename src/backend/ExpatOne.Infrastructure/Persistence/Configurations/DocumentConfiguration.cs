using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(d => d.OriginalFileName)
            .HasMaxLength(300);

        builder.Property(d => d.S3ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.S3BucketName)
            .HasMaxLength(100);

        builder.Property(d => d.ContentType)
            .HasMaxLength(100);

        builder.Property(d => d.IssuingAuthority)
            .HasMaxLength(200);

        builder.Property(d => d.DocumentNumber)
            .HasMaxLength(100);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DocumentType)
            .WithMany()
            .HasForeignKey(d => d.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => d.ExpiryDate);
    }
}
