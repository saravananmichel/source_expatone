using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

/// <summary>Durable, idempotent HR inbox alert for a verified issued permit.</summary>
public sealed class CorporatePermitAlert : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid PermitId { get; set; }
    public int ThresholdDays { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
}
