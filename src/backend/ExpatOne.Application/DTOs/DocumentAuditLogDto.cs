namespace ExpatOne.Application.DTOs;

public class DocumentAuditLogDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string Action { get; set; }
    public Guid? TargetVersionId { get; set; }
    public Guid? TargetShareId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}
