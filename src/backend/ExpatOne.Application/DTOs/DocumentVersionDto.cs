namespace ExpatOne.Application.DTOs;

public class DocumentVersionDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UploadVersionDto
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
}

public class VersionUploadUrlResponseDto
{
    public Guid VersionId { get; set; }
    public required string UploadUrl { get; set; }
}
