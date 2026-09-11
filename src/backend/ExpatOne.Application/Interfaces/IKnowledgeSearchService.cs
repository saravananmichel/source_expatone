namespace ExpatOne.Application.Interfaces;

public interface IKnowledgeSearchService
{
    Task<List<KnowledgeSearchResult>> SearchAsync(string query, string countryCode = "MY", int maxResults = 5);
}

public class KnowledgeSearchResult
{
    public Guid KnowledgeId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string? SourceUrl { get; set; }
    public string? Department { get; set; }
    public DateTime? LastVerifiedDate { get; set; }
    public double RelevanceScore { get; set; }
}
