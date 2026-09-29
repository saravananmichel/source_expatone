using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public required string S3ObjectKey { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Sha256Hash { get; set; }
    public bool IsCurrent { get; set; }
    public Guid UploadedByUserId { get; set; }

    public Document Document { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
