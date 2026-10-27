namespace ExpatOne.Domain.Entities;

public sealed class CorporateCompanyProfile
{
    public Guid CompanyId { get; set; }
    public string? LogoObjectKey { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public string? ContactNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? Website { get; set; }
    public string OfferTemplateJson { get; set; } = "{}";
    public int TemplateVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CorporateBenefit
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Currency
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NumericCode { get; set; }
    public short? MinorUnits { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
