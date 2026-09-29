namespace ExpatOne.Application.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string CountryCode { get; set; } = "MY";
    public string PreferredLanguage { get; set; } = "en";
    public string ExternalProvider { get; set; } = "firebase";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Nationality { get; set; }
    public string? ResidenceLocation { get; set; }
    public string? VisaPassType { get; set; }
    public string? EmploymentStatus { get; set; }
    public string? FamilyStatus { get; set; }
    public bool? HasChildren { get; set; }
    public int? NumberOfChildren { get; set; }
    public bool OnboardingCompleted { get; set; }
}

public class UpdateUserDto
{
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? Nationality { get; set; }
    public string? ResidenceLocation { get; set; }
    public string? VisaPassType { get; set; }
    public string? EmploymentStatus { get; set; }
    public string? FamilyStatus { get; set; }
    public bool? HasChildren { get; set; }
    public int? NumberOfChildren { get; set; }
    public bool? OnboardingCompleted { get; set; }
}
