namespace ExpatOne.Application.Common;

public class AIProviderUnavailableException : Exception
{
    public AIProviderUnavailableException(string message) : base(message) { }
    public AIProviderUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
