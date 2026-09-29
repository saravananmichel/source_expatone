using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class DocumentAuditLog : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public required string Action { get; set; }
    public Guid? TargetVersionId { get; set; }
    public Guid? TargetShareId { get; set; }
    public string? Metadata { get; set; }

    public Document Document { get; set; } = null!;
    public User User { get; set; } = null!;
}
