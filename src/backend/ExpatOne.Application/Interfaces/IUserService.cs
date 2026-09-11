using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto?> GetByExternalIdAsync(string externalProvider, string externalId);
    Task<UserDto> FindOrCreateByExternalIdentityAsync(string externalProvider, string externalId, string email, string? displayName);
    Task<UserDto> UpdateProfileAsync(Guid id, UpdateUserDto dto);
}
