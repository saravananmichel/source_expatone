using ExpatOne.Domain.Common;
using Pgvector;

namespace ExpatOne.Domain.Entities;

public class GovernmentKnowledge : BaseEntity
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? SourceUrl { get; set; }
    public string? Department { get; set; }
    public string? Category { get; set; }
    public string CountryCode { get; set; } = "MY";
    public DateTime? EffectiveDate { get; set; }
    public DateTime? LastVerifiedDate { get; set; }
    public string? Version { get; set; }

    public Vector? Embedding { get; set; }
    public string? ContentHash { get; set; }
    public int ChunkIndex { get; set; }
    public DateTime? EmbeddedAt { get; set; }

    public Guid? GovernmentSourceId { get; set; }
    public GovernmentSource? GovernmentSource { get; set; }
}
