using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Tests;
public class DocumentAnalysisConfigurationTests
{
    [Theory]
    [InlineData("Local", false)]
    public void QueuedProviderAndFallbackAreRecoverable(string provider, bool fallback)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["DocumentIntelligence:Provider"] = provider, ["DocumentIntelligence:EnableFallback"] = fallback.ToString()
        }).Build();
        var encoded = DocumentAnalysisConfiguration.Fingerprint(config);
        var decoded = DocumentAnalysisConfiguration.Decode(encoded);
        Assert.Equal(provider, decoded.Provider); Assert.Equal(fallback, decoded.Fallback);
        Assert.True(encoded.Length < 120);
    }
    [Theory]
    [InlineData("Gemini",false)][InlineData("Hybrid",false)][InlineData("Local",true)]
    public void UnsafeProvidersCannotBeQueued(string provider,bool fallback)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["DocumentIntelligence:Provider"]=provider,["DocumentIntelligence:EnableFallback"]=fallback.ToString() }).Build();
        Assert.Throws<InvalidOperationException>(()=>DocumentAnalysisConfiguration.Fingerprint(config));
    }
    [Fact]
    public void LegacyFingerprintsRemainCompatible()
    {
        Assert.Null(DocumentAnalysisConfiguration.Decode("legacy-v1").Provider);
    }
}
