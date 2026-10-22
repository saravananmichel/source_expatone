namespace ExpatOne.Application.Common;

public sealed class CorporateAuthorizationException : Exception
{
    public CorporateAuthorizationException()
        : base("Corporate access denied.")
    {
    }
}
