using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface ITranslationService
{
    Task<TranslationResultDto> TranslateAsync(TranslateRequestDto request);
    IReadOnlyList<SupportedLanguageDto> GetSupportedLanguages();
}

public class SupportedLanguageDto
{
    public required string Code { get; set; }
    public required string Name { get; set; }
}
