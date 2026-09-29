using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Services;

public class ProfileService : IProfileService
{
    private readonly ExpatOneDbContext _dbContext;

    public ProfileService(ExpatOneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ProfileOptionsDto GetProfileOptions()
    {
        return new ProfileOptionsDto
        {
            VisaPassTypes =
            [
                new() { Value = nameof(VisaPassType.EmploymentPass), Label = "Employment Pass" },
                new() { Value = nameof(VisaPassType.DependantPass), Label = "Dependant Pass" },
                new() { Value = nameof(VisaPassType.MM2H), Label = "MM2H (Malaysia My Second Home)" },
                new() { Value = nameof(VisaPassType.StudentPass), Label = "Student Pass" },
                new() { Value = nameof(VisaPassType.ProfessionalVisitPass), Label = "Professional Visit Pass" },
                new() { Value = nameof(VisaPassType.LongTermSocialVisitPass), Label = "Long-Term Social Visit Pass" },
                new() { Value = nameof(VisaPassType.Other), Label = "Other" },
            ],
            EmploymentStatuses =
            [
                new() { Value = nameof(EmploymentStatus.Employed), Label = "Employed" },
                new() { Value = nameof(EmploymentStatus.SelfEmployed), Label = "Self-Employed" },
                new() { Value = nameof(EmploymentStatus.Unemployed), Label = "Unemployed" },
                new() { Value = nameof(EmploymentStatus.Student), Label = "Student" },
                new() { Value = nameof(EmploymentStatus.Retired), Label = "Retired" },
                new() { Value = nameof(EmploymentStatus.Other), Label = "Other" },
            ],
            FamilyStatuses =
            [
                new() { Value = nameof(FamilyStatus.Single), Label = "Single" },
                new() { Value = nameof(FamilyStatus.Married), Label = "Married" },
                new() { Value = nameof(FamilyStatus.MarriedWithFamily), Label = "Married with Family" },
                new() { Value = nameof(FamilyStatus.Other), Label = "Other" },
            ],
            SupportedLanguages =
            [
                new() { Value = "en", Label = "English" },
                new() { Value = "ms", Label = "Bahasa Malaysia" },
                new() { Value = "zh", Label = "Chinese (Simplified)" },
                new() { Value = "ta", Label = "Tamil" },
                new() { Value = "hi", Label = "Hindi" },
                new() { Value = "ar", Label = "Arabic" },
                new() { Value = "ja", Label = "Japanese" },
                new() { Value = "ko", Label = "Korean" },
            ],
        };
    }

    public async Task<List<ChecklistItemDto>> GetOnboardingChecklistAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null) return [];

        var userDocTypes = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => d.DocumentType!.Name)
            .ToListAsync();

        var items = new List<ChecklistItemDto>();

        items.Add(new ChecklistItemDto
        {
            Id = "passport",
            Category = "Documents",
            Title = "Upload your passport",
            Description = "Keep a secure digital copy of your passport in your document wallet.",
            IsCompleted = userDocTypes.Contains("Passport"),
            Action = "documents",
        });

        if (user.VisaPassType is not null)
        {
            var visaLabel = GetVisaLabel(user.VisaPassType);
            items.Add(new ChecklistItemDto
            {
                Id = "visa-document",
                Category = "Documents",
                Title = $"Upload your {visaLabel}",
                Description = $"Store your {visaLabel} document for easy access and expiry tracking.",
                IsCompleted = userDocTypes.Contains("Visa") || userDocTypes.Contains("Employment Pass"),
                Action = "documents",
            });
        }

        if (user.EmploymentStatus is "Employed" or "SelfEmployed")
        {
            items.Add(new ChecklistItemDto
            {
                Id = "employment-docs",
                Category = "Employment",
                Title = "Review employment requirements",
                Description = "Check your employment-related document and compliance needs.",
                IsCompleted = userDocTypes.Contains("Work Permit") || userDocTypes.Contains("Employment Pass"),
                Action = "assistant",
            });
        }

        if (user.HasChildren == true)
        {
            items.Add(new ChecklistItemDto
            {
                Id = "family-docs",
                Category = "Family",
                Title = "Review family document requirements",
                Description = "Check dependant pass and education requirements for your family.",
                IsCompleted = false,
                Action = "assistant",
            });
        }

        items.Add(new ChecklistItemDto
        {
            Id = "explore-assistant",
            Category = "Getting Started",
            Title = "Explore the Government Assistant",
            Description = "Ask questions about visas, taxes, driving, and more.",
            IsCompleted = await _dbContext.AIConversations.AnyAsync(c => c.UserId == userId),
            Action = "assistant",
        });

        items.Add(new ChecklistItemDto
        {
            Id = "setup-reminders",
            Category = "Getting Started",
            Title = "Set up document reminders",
            Description = "Never miss a passport or visa expiry date.",
            IsCompleted = await _dbContext.Reminders.AnyAsync(r => r.UserId == userId),
            Action = "reminders",
        });

        return items;
    }

    private static string GetVisaLabel(string visaPassType) => visaPassType switch
    {
        nameof(VisaPassType.EmploymentPass) => "Employment Pass",
        nameof(VisaPassType.DependantPass) => "Dependant Pass",
        nameof(VisaPassType.MM2H) => "MM2H visa",
        nameof(VisaPassType.StudentPass) => "Student Pass",
        nameof(VisaPassType.ProfessionalVisitPass) => "Professional Visit Pass",
        nameof(VisaPassType.LongTermSocialVisitPass) => "Long-Term Social Visit Pass",
        _ => "visa/pass",
    };
}
