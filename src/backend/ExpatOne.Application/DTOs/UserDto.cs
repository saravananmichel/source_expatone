namespace ExpatOne.Application.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public string CountryCode { get; set; } = "MY";
    public string PreferredLanguage { get; set; } = "en";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserDto
{
    public required string ExternalId { get; set; }
    public string ExternalProvider { get; set; } = "firebase";
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string CountryCode { get; set; } = "MY";
    public string PreferredLanguage { get; set; } = "en";
}
