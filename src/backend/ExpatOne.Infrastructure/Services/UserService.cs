using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpatOne.Infrastructure.Services;

public class UserService : IUserService
{
    private static readonly HashSet<string> SupportedLanguages =
        ["en", "ms", "zh", "ta", "hi", "ar", "ja", "ko"];

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
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent first-login: another request created this user between our read and write.
            // The unique composite index prevented a duplicate — re-query to return the winner.
            _dbContext.ChangeTracker.Clear();
            user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.ExternalProvider == externalProvider && u.ExternalId == externalId)
                ?? throw new InvalidOperationException("Failed to create or retrieve user account.");
        }

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
        {
            if (!SupportedLanguages.Contains(dto.PreferredLanguage))
                throw new ArgumentException($"Unsupported language: {dto.PreferredLanguage}");
            user.PreferredLanguage = dto.PreferredLanguage;
        }

        if (dto.Nationality is not null)
        {
            if (dto.Nationality.Length < 2 || dto.Nationality.Length > 5)
                throw new ArgumentException("Nationality must be a 2-5 character country code.");
            user.Nationality = dto.Nationality;
        }

        if (dto.ResidenceLocation is not null)
            user.ResidenceLocation = dto.ResidenceLocation;

        if (dto.VisaPassType is not null)
        {
            if (!Enum.TryParse<VisaPassType>(dto.VisaPassType, ignoreCase: true, out _))
                throw new ArgumentException($"Invalid visa/pass type: {dto.VisaPassType}");
            user.VisaPassType = dto.VisaPassType;
        }

        if (dto.EmploymentStatus is not null)
        {
            if (!Enum.TryParse<EmploymentStatus>(dto.EmploymentStatus, ignoreCase: true, out _))
                throw new ArgumentException($"Invalid employment status: {dto.EmploymentStatus}");
            user.EmploymentStatus = dto.EmploymentStatus;
        }

        if (dto.FamilyStatus is not null)
        {
            if (!Enum.TryParse<FamilyStatus>(dto.FamilyStatus, ignoreCase: true, out _))
                throw new ArgumentException($"Invalid family status: {dto.FamilyStatus}");
            user.FamilyStatus = dto.FamilyStatus;
        }

        if (dto.HasChildren is not null)
            user.HasChildren = dto.HasChildren;

        if (dto.NumberOfChildren is not null)
        {
            if (dto.NumberOfChildren < 0 || dto.NumberOfChildren > 20)
                throw new ArgumentException("Number of children must be between 0 and 20.");
            user.NumberOfChildren = dto.NumberOfChildren;
        }

        if (dto.OnboardingCompleted is not null)
            user.OnboardingCompleted = dto.OnboardingCompleted.Value;

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
        CreatedAt = user.CreatedAt,
        Nationality = user.Nationality,
        ResidenceLocation = user.ResidenceLocation,
        VisaPassType = user.VisaPassType,
        EmploymentStatus = user.EmploymentStatus,
        FamilyStatus = user.FamilyStatus,
        HasChildren = user.HasChildren,
        NumberOfChildren = user.NumberOfChildren,
        OnboardingCompleted = user.OnboardingCompleted
    };
}
