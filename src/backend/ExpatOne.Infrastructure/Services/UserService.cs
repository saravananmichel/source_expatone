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

    public async Task<UserDto?> GetByExternalIdAsync(string externalProvider, string externalId)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == externalProvider && u.ExternalId == externalId);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> FindOrCreateByExternalIdentityAsync(
        string externalProvider, string externalId, string email, string? displayName)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == externalProvider && u.ExternalId == externalId);

        if (user is not null)
        {
            var updated = false;
            if (user.Email != email)
            {
                user.Email = email;
                updated = true;
            }
            if (displayName is not null && user.DisplayName != displayName)
            {
                user.DisplayName = displayName;
                updated = true;
            }
            if (updated)
                await _dbContext.SaveChangesAsync();

            return MapToDto(user);
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            ExternalProvider = externalProvider,
            Email = email,
            DisplayName = displayName,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return MapToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _dbContext.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        if (dto.DisplayName is not null)
            user.DisplayName = dto.DisplayName;
        if (dto.PhoneNumber is not null)
            user.PhoneNumber = dto.PhoneNumber;
        if (dto.CountryCode is not null)
            user.CountryCode = dto.CountryCode;
        if (dto.PreferredLanguage is not null)
            user.PreferredLanguage = dto.PreferredLanguage;

        await _dbContext.SaveChangesAsync();
        return MapToDto(user);
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        PhoneNumber = user.PhoneNumber,
        CountryCode = user.CountryCode,
        PreferredLanguage = user.PreferredLanguage,
        ExternalProvider = user.ExternalProvider,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
