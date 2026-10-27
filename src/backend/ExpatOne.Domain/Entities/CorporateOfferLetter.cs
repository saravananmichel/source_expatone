using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public sealed class CorporateOfferLetter : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeUserId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public string TemplateDataJson { get; set; } = "{}";
    public Guid? DocumentId { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Response { get; set; }
}
