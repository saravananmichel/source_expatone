using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class AIConversationMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public string? StructuredResponse { get; set; }
    public string? SourceReferences { get; set; }

    public AIConversation Conversation { get; set; } = null!;
}
