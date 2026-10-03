namespace ExpatOne.Application.DTOs;

public class DocumentAnalysisDto
{
    public Guid? DocumentVersionId { get; set; }
    public Guid? AnalysisId { get; set; }
    public string? ConfigurationVersion { get; set; }
    public string? PromptVersion { get; set; }
    public string? Provider { get; set; }
    public string? ModelVersion { get; set; }
    public string AnalyzerVersion { get; set; } = "document-engine-v1";
    public bool RequiresReview { get; set; }
    public List<GroundedStatement> Statements { get; set; } = [];
    public List<DocumentEvidenceDto> Evidence { get; set; } = [];
    public System.Text.Json.JsonElement? SemanticDocument { get; set; }
    public List<AnalysisSectionDto> AnalysisSections { get; set; } = [];
    public List<string> MissingInformation { get; set; } = [];
    public string? ExplanationLanguage { get; set; }
    public System.Text.Json.JsonElement? QualityDiagnostics { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentCategory { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Title { get; set; }
    public string? PersonName { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? DocumentNumber { get; set; }
    public string? IssueDate { get; set; }
    public string? ExpiryDate { get; set; }
    public string? EffectiveDate { get; set; }
    public string? DocumentStatus { get; set; }
    public string? PlainLanguageExplanation { get; set; }
    public string? Confidence { get; set; }
    public List<ExtractedDateItem> ImportantDates { get; set; } = [];
    public List<string> Deadlines { get; set; } = [];
    public List<string> RequiredActions { get; set; } = [];
    public List<KeyInfoItem> KeyInformation { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<TermExplanation> Terminology { get; set; } = [];
    public DateTime AnalyzedAt { get; set; }
}

public class ExtractedDateItem
{
    public string Label { get; set; } = "";
    public string Date { get; set; } = "";
    public bool IsExtracted { get; set; }
}

public class KeyInfoItem
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsExtracted { get; set; }
}

public class TermExplanation
{
    public string Term { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public class AnalyzeRequestDto
{
    public bool ForceReanalyze { get; set; }
}

public class DocumentQuestionDto
{
    public required string Question { get; set; }
}

public class DocumentAnswerDto
{
    public List<AnalysisSectionDto> AnalysisSections { get; set; } = [];
    public List<string> MissingInformation { get; set; } = [];
    public string? ExplanationLanguage { get; set; }
    public System.Text.Json.JsonElement? QualityDiagnostics { get; set; }
    public Guid DocumentId { get; set; }
    public required string Answer { get; set; }
    public bool Grounded { get; set; }
    public string? DocumentName { get; set; }
}
