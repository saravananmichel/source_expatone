using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpatOne.Infrastructure.Persistence.Configurations;

public class AIConversationMessageConfiguration : IEntityTypeConfiguration<AIConversationMessage>
{
    public void Configure(EntityTypeBuilder<AIConversationMessage> builder)
    {
        builder.ToTable("ai_conversation_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.HasIndex(m => m.ConversationId);
    }
}
