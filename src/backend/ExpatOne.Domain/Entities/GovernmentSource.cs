using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class GovernmentSource : BaseEntity
{
    public required string Name { get; set; }
    public string? Url { get; set; }
    public string? Department { get; set; }
    public string CountryCode { get; set; } = "MY";
    public bool IsActive { get; set; } = true;
    public DateTime? LastScrapedDate { get; set; }
}
