using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class GovernmentKnowledgeConfiguration : IEntityTypeConfiguration<GovernmentKnowledge>
{
    public void Configure(EntityTypeBuilder<GovernmentKnowledge> builder)
    {
        builder.ToTable("government_knowledge");

        builder.HasKey(gk => gk.Id);

        builder.Property(gk => gk.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(gk => gk.Content)
            .IsRequired();

        builder.Property(gk => gk.SourceUrl)
            .HasMaxLength(500);

        builder.Property(gk => gk.Department)
            .HasMaxLength(200);

        builder.Property(gk => gk.Category)
            .HasMaxLength(100);

        builder.Property(gk => gk.CountryCode)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(gk => gk.Version)
            .HasMaxLength(50);

        builder.Property(gk => gk.ContentHash)
            .HasMaxLength(64);

        builder.Property(gk => gk.Embedding)
            .HasColumnType("vector(768)");

        builder.HasOne(gk => gk.GovernmentSource)
            .WithMany()
            .HasForeignKey(gk => gk.GovernmentSourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(gk => gk.CountryCode);
        builder.HasIndex(gk => gk.Category);
        builder.HasIndex(gk => gk.GovernmentSourceId);

        builder.HasIndex(gk => gk.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
