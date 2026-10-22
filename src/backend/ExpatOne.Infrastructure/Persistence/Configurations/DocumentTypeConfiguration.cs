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

        builder.HasData(
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), Name = "Passport", Description = "International travel document", Category = "Identity", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), Name = "Visa", Description = "Entry/stay permit", Category = "Immigration", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8500), Name = "Employment Pass", Description = "Work authorization", Category = "Immigration", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Driving Licence", Description = "Driving authorization", Category = "Identity", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Insurance", Description = "Insurance policy document", Category = "Insurance", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Medical Card", Description = "Health/medical card", Category = "Medical", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000007"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Work Permit", Description = "Work authorization permit", Category = "Immigration", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000008"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Government Letter", Description = "Official government correspondence", Category = "Government", HasExpiry = false, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000009"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Other", Description = "Other document type", Category = "General", HasExpiry = false, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000010"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Immigration Document", Description = "Immigration-related document", Category = "Immigration", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000011"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Employment Contract", Description = "Employment agreement or offer letter", Category = "Employment", HasExpiry = false, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000012"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8510), Name = "Rental Agreement", Description = "Tenancy or rental agreement", Category = "Housing", HasExpiry = true, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000013"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), Name = "Tax Document", Description = "Tax assessment, return, or receipt", Category = "Financial", HasExpiry = false, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000014"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), Name = "Government Correspondence", Description = "General government correspondence", Category = "Government", HasExpiry = false, IsSystem = true },
            new DocumentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000015"), CreatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), UpdatedAt = new DateTime(2026, 9, 17, 17, 20, 33, 257, DateTimeKind.Utc).AddTicks(8520), Name = "General Correspondence", Description = "Non-government correspondence or letter", Category = "General", HasExpiry = false, IsSystem = true }
        );
    }
}
