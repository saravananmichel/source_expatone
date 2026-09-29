using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class DocumentShare : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid SharedWithUserId { get; set; }
    public string Permission { get; set; } = "Read";
    public DateTime? RevokedAt { get; set; }

    public Document Document { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
    public User SharedWithUser { get; set; } = null!;
}
