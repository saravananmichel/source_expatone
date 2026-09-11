using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class EmergencyResource : BaseEntity
{
    public required string Name { get; set; }
    public required string Category { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CountryCode { get; set; } = "MY";
    public string? State { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; } = true;
}
