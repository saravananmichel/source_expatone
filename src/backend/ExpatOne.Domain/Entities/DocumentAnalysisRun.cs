using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

// Immutable input snapshot; one row per analysis attempt/version, including durable job state.
public class DocumentAnalysisRun : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public required string ObjectKey { get; set; }
    public required string ContentType { get; set; }
    public required string ConfigurationVersion { get; set; }
    public string Status { get; set; } = "QUEUED";
    public string Stage { get; set; } = "Queued";
    public int Attempts { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public string? ResultJson { get; set; }
    public string? ContentHash { get; set; }
    public string? ErrorCategory { get; set; }
    public DateTime? CompletedAt { get; set; }
}
