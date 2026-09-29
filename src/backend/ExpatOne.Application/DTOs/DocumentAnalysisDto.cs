namespace ExpatOne.Application.DTOs;

public class DocumentAnalysisDto
{
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
    public Guid DocumentId { get; set; }
    public required string Answer { get; set; }
    public bool Grounded { get; set; }
    public string? DocumentName { get; set; }
}
