using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class AIConversationConfiguration : IEntityTypeConfiguration<AIConversation>
{
    public void Configure(EntityTypeBuilder<AIConversation> builder)
    {
        builder.ToTable("ai_conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Module)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Title)
            .HasMaxLength(300);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId);
    }
}
