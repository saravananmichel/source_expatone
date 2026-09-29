using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IAssistantService
{
    Task<ConversationDto> CreateConversationAsync(Guid userId, string countryCode = "MY");
    Task<List<ConversationSummaryDto>> GetConversationsAsync(Guid userId);
    Task<ConversationDto?> GetConversationAsync(Guid conversationId, Guid userId);
    Task<AssistantMessageDto> SendMessageAsync(Guid conversationId, Guid userId, string message);
    Task DeleteConversationAsync(Guid conversationId, Guid userId);
}
