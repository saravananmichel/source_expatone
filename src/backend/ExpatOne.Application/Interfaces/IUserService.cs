using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto?> GetByExternalIdAsync(string externalId);
    Task<UserDto> CreateAsync(CreateUserDto dto);
}
