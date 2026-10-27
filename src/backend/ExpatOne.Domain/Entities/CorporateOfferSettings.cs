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

public sealed class CorporateOfferBenefit
{
    public Guid OfferId { get; set; }
    public Guid? BenefitId { get; set; }
    public int Position { get; set; }
    public string CategorySnapshot { get; set; } = "";
    public string NameSnapshot { get; set; } = "";
    public string? DescriptionSnapshot { get; set; }
}

public sealed class CorporateOfferSignature
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public Guid EmployeeUserId { get; set; }
    public string SignatureObjectKey { get; set; } = "";
    public Guid? SignedDocumentId { get; set; }
    public DateTime SignedAt { get; set; }
    public string SignatureMethod { get; set; } = "Drawn";
    public int OfferVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
