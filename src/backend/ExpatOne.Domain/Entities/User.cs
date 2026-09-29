using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class User : BaseEntity
{
    public required string ExternalId { get; set; }
    public string ExternalProvider { get; set; } = "firebase";
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string CountryCode { get; set; } = "MY";
    public string PreferredLanguage { get; set; } = "en";
    public bool IsActive { get; set; } = true;

    public string? Nationality { get; set; }
    public string? ResidenceLocation { get; set; }
    public string? VisaPassType { get; set; }
    public string? EmploymentStatus { get; set; }
    public string? FamilyStatus { get; set; }
    public bool? HasChildren { get; set; }
    public int? NumberOfChildren { get; set; }
    public bool OnboardingCompleted { get; set; }
}
