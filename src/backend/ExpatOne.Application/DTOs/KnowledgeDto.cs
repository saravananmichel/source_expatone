namespace ExpatOne.Application.DTOs;

public class GovernmentSourceDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Url { get; set; }
    public string? Department { get; set; }
    public string CountryCode { get; set; } = "MY";
    public bool IsActive { get; set; }
    public DateTime? LastIngestedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RegisterSourceDto
{
    public required string Name { get; set; }
    public string? Url { get; set; }
    public string? Department { get; set; }
    public string CountryCode { get; set; } = "MY";
}

public class IngestContentDto
{
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? Category { get; set; }
}

public class IngestionResultDto
{
    public Guid SourceId { get; set; }
    public int ChunksCreated { get; set; }
    public bool ContentChanged { get; set; }
}

public class EmbeddingGenerationResultDto
{
    public int EmbeddingsGenerated { get; set; }
    public int TotalPending { get; set; }
}
