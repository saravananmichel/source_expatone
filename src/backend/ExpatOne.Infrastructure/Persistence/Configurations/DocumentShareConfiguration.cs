using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class DocumentShareConfiguration : IEntityTypeConfiguration<DocumentShare>
{
    public void Configure(EntityTypeBuilder<DocumentShare> builder)
    {
        builder.ToTable("document_shares");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Permission)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasOne(s => s.Document)
            .WithMany(d => d.Shares)
            .HasForeignKey(s => s.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.OwnerUser)
            .WithMany()
            .HasForeignKey(s => s.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SharedWithUser)
            .WithMany()
            .HasForeignKey(s => s.SharedWithUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.DocumentId, s.SharedWithUserId, s.RevokedAt });
        builder.HasIndex(s => s.SharedWithUserId);
    }
}
