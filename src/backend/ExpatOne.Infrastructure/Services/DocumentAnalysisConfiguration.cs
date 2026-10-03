using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Infrastructure.Services;

public static class DocumentAnalysisConfiguration
{
    public static string Fingerprint(IConfiguration config)
    {
        var provider = config["DocumentIntelligence:Provider"] ?? "Local";
        if (provider != "Local") throw new InvalidOperationException("Document AI requires Local provider.");
        var fallback = string.Equals(config["DocumentIntelligence:EnableFallback"], "true", StringComparison.OrdinalIgnoreCase);
        if (fallback) throw new InvalidOperationException("Document AI fallback must be disabled.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
            config["DocumentIntelligence:ConfigurationVersion"] ?? "local-v2", provider, fallback))));
        // Provider choice travels with the queued run, independent of which worker claims it.
        return $"{hash}:{provider}:{(fallback ? "1" : "0")}";
    }
    public static (string? Provider, bool? Fallback) Decode(string fingerprint)
    {
        var parts = fingerprint.Split(':');
        return parts.Length == 3 && parts[1] is "Local" or "Gemini" or "Hybrid" && parts[2] is "0" or "1"
            ? (parts[1], parts[2] == "1") : (null, null); // Preserve pre-v2 queued jobs.
    }
}
