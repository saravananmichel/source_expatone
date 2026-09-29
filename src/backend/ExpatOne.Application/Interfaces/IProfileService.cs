using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IProfileService
{
    ProfileOptionsDto GetProfileOptions();
    Task<List<ChecklistItemDto>> GetOnboardingChecklistAsync(Guid userId);
}
