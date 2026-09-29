using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IReminderService
{
    Task<List<ReminderDto>> GetUserRemindersAsync(Guid userId);
    Task<ReminderDto?> GetReminderAsync(Guid userId, Guid reminderId);
    Task<ReminderDto> CreateReminderAsync(Guid userId, CreateReminderDto dto);
    Task<List<ReminderDto>> GenerateRemindersAsync(Guid userId, GenerateRemindersDto dto);
    Task<ReminderDto> UpdateReminderAsync(Guid userId, Guid reminderId, UpdateReminderDto dto);
    Task DeleteReminderAsync(Guid userId, Guid reminderId);
    Task<List<ReminderDto>> GetDocumentRemindersAsync(Guid userId, Guid documentId);
    Task ProcessDueRemindersAsync();
}
