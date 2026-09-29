namespace ExpatOne.Application.DTOs;

public class CreateConversationDto
{
    public string CountryCode { get; set; } = "MY";
}

public class SendMessageDto
{
    public required string Message { get; set; }
}

public class ConversationDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string Module { get; set; } = "government-assistant";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AssistantMessageDto> Messages { get; set; } = [];
}

public class ConversationSummaryDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AssistantMessageDto
{
    public Guid Id { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public List<MessageSourceDto>? Sources { get; set; }
    // "official_grounded" | "general_unverified" | "casual" — null for user messages and legacy history
    public string? ResponseMode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MessageSourceDto
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Department { get; set; }
    public string? Category { get; set; }
    public string? CountryCode { get; set; }
    public double RelevanceScore { get; set; }
}
