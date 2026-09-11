namespace ExpatOne.Application.Common;

public class ApiErrorResponse
{
    public required string Message { get; set; }
    public string? Code { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}
