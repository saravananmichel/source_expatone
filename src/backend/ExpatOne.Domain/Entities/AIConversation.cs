using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class AIConversation : BaseEntity
{
    public Guid UserId { get; set; }
    public required string Module { get; set; }
    public string? Title { get; set; }

    public User User { get; set; } = null!;
    public List<AIConversationMessage> Messages { get; set; } = [];
}
