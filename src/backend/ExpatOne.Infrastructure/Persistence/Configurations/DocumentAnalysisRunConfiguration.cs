using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ExpatOne.Infrastructure.Persistence.Configurations;
public class DocumentAnalysisRunConfiguration : IEntityTypeConfiguration<DocumentAnalysisRun>
{
    public void Configure(EntityTypeBuilder<DocumentAnalysisRun> b)
    {
        b.ToTable("document_analysis_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ObjectKey).HasMaxLength(500);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.ConfigurationVersion).HasMaxLength(200);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.Stage).HasMaxLength(100);
        b.Property(x => x.ErrorCategory).HasMaxLength(100);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.DocumentId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.LeaseUntil });
        b.HasIndex(x => new { x.DocumentId, x.ObjectKey, x.ConfigurationVersion }).IsUnique()
            .HasFilter("\"Status\" IN ('QUEUED', 'PROCESSING')");
    }
}
