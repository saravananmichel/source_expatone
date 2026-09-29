namespace ExpatOne.Application.DTOs;

public class TranslateRequestDto
{
    public required string Text { get; set; }
    public string SourceLanguage { get; set; } = "auto";
    public required string TargetLanguage { get; set; }
}

public class TranslationResultDto
{
    public required string TranslatedText { get; set; }
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }
}
