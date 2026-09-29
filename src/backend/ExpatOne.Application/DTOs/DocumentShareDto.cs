namespace ExpatOne.Application.DTOs;

public class DocumentShareDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string? DocumentName { get; set; }
    public Guid SharedWithUserId { get; set; }
    public string? SharedWithEmail { get; set; }
    public string Permission { get; set; } = "Read";
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class CreateShareDto
{
    public required string SharedWithEmail { get; set; }
}

public class SharedDocumentDto
{
    public Guid ShareId { get; set; }
    public Guid DocumentId { get; set; }
    public required string DocumentName { get; set; }
    public required string DocumentType { get; set; }
    public string? OwnerEmail { get; set; }
    public string Permission { get; set; } = "Read";
    public DateTime? ExpiryDate { get; set; }
    public DateTime SharedAt { get; set; }
}
