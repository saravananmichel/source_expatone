namespace ExpatOne.Application.DTOs;
public record AnalysisJobDto(Guid AnalysisId, Guid DocumentId, Guid? DocumentVersionId, string Status,
    string Stage, DateTime CreatedAt, string? ErrorCategory, DocumentAnalysisDto? Analysis);
public class GroundedStatement
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "fact";
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public string? OriginalValue { get; set; }
    public string? NormalizedValue { get; set; }
    public string SupportStatus { get; set; } = "UNCERTAIN";
    public string? SupportReason { get; set; }
    public double Confidence { get; set; }
    public List<string> EvidenceIds { get; set; } = [];
}
public class DocumentEvidenceDto
{
    public string Id { get; set; } = "";
    public int Page { get; set; }
    public string? Section { get; set; }
    public string SourceText { get; set; } = "";
    public double[]? BoundingBox { get; set; }
}

public class AnalysisSectionDto
{
    public string Title { get; set; } = "";
    public List<string> StatementIds { get; set; } = [];
}
