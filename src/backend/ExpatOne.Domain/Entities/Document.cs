using ExpatOne.Domain.Common;
using ExpatOne.Domain.Enums;

namespace ExpatOne.Domain.Entities;

public class Document : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public required string DocumentName { get; set; }
    public string? OriginalFileName { get; set; }
    public required string S3ObjectKey { get; set; }
    public string? S3BucketName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ContentType { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? DocumentNumber { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public string? ExtractedMetadata { get; set; }

    public User User { get; set; } = null!;
    public DocumentType DocumentType { get; set; } = null!;
}
