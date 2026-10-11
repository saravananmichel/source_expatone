using ExpatOne.Domain.Common;
namespace ExpatOne.Domain.Entities;

public static class FamilyRequestStates
{
    public const int Pending = 1, Accepted = 2, Rejected = 3, Cancelled = 4, Revoked = 5;
}
public class FamilyRequestStatus
{
    public int Id { get; set; }
    public required string Name { get; set; }
}
public class FamilyRequest : BaseEntity
{
    public Guid RequesterUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public int StatusId { get; set; } = FamilyRequestStates.Pending;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public DateTime? RespondedAt { get; set; }
    public User Requester { get; set; } = null!;
    public User Recipient { get; set; } = null!;
    public FamilyRequestStatus Status { get; set; } = null!;
    public ICollection<FamilyDocumentGrant> Grants { get; set; } = [];
}
public class FamilyDocumentGrant
{
    public Guid FamilyRequestId { get; set; }
    public Guid DocumentId { get; set; }
    public FamilyRequest Request { get; set; } = null!;
    public Document Document { get; set; } = null!;
}
