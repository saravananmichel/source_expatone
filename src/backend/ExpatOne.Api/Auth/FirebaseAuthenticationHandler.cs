using System.Security.Claims;
using System.Text.Encodings.Web;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ExpatOne.Api.Auth;

public class FirebaseAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Firebase";

    public FirebaseAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        try
        {
            var firebaseToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, firebaseToken.Uid),
                new("firebase_uid", firebaseToken.Uid),
            };

            if (firebaseToken.Claims.TryGetValue("email", out var email))
                claims.Add(new Claim(ClaimTypes.Email, email.ToString()!));

            if (firebaseToken.Claims.TryGetValue("name", out var name))
                claims.Add(new Claim(ClaimTypes.Name, name.ToString()!));

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return AuthenticateResult.Success(ticket);
        }
        catch (FirebaseAuthException)
        {
            return AuthenticateResult.Fail("Invalid Firebase token.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Firebase token validation failed");
            return AuthenticateResult.Fail("Token validation failed.");
        }
    }
}
