using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

/// <summary>HR-verified issued permit metadata, separate from visa application drafts.</summary>
public sealed class CorporateIssuedPermit : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeUserId { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string CountryCode { get; set; } = "";
    public string PermitType { get; set; } = "";
    public string Status { get; set; } = "Active";
    public DateTime? IssuedOn { get; set; }
    public DateTime ExpiresOn { get; set; }
    public string? Reference { get; set; }
}
