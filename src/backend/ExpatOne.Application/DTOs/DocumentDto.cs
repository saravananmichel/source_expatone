namespace ExpatOne.Application.DTOs;

public class DocumentDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string DocumentType { get; set; }
    public Guid DocumentTypeId { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DocumentTypeDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool HasExpiry { get; set; }
}

public class RequestUploadDto
{
    public Guid DocumentTypeId { get; set; }
    public required string DocumentName { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class UploadUrlResponseDto
{
    public Guid DocumentId { get; set; }
    public required string UploadUrl { get; set; }
    public required string ObjectKey { get; set; }
}

public class AccessUrlResponseDto
{
    public required string Url { get; set; }
    public int ExpiresInSeconds { get; set; }
}
