using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly ExpatOneDbContext _dbContext;

    public UserService(ExpatOneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto?> GetByExternalIdAsync(string externalId)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalId == externalId);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = dto.ExternalId,
            ExternalProvider = dto.ExternalProvider,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            PhoneNumber = dto.PhoneNumber,
            CountryCode = dto.CountryCode,
            PreferredLanguage = dto.PreferredLanguage
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return MapToDto(user);
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        CountryCode = user.CountryCode,
        PreferredLanguage = user.PreferredLanguage,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
