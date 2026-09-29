namespace ExpatOne.Application.DTOs;

public class EmergencyAssistRequestDto
{
    public required string Message { get; set; }
    public string? TargetLanguage { get; set; }
}

public class EmergencyAssistResponseDto
{
    public required string Response { get; set; }
    public string? TranslatedMessage { get; set; }
    public string? TargetLanguage { get; set; }
}
