namespace ExpatOne.Infrastructure.Services;

/// <summary>
/// Selects the document object-storage provider. An explicit Storage:Provider setting
/// wins; otherwise Cloud Run and AWS container markers are used for environment detection.
/// </summary>
public static class StorageProviderResolver
{
    public const string Aws = "aws";
    public const string Gcp = "gcp";

    public static string Resolve(
        string? configuredProvider,
        string? cloudRunService,
        string? awsExecutionEnvironment,
        string? ecsMetadataUri,
        string? awsRegion)
    {
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            var configured = configuredProvider.Trim().ToLowerInvariant();
            return configured switch
            {
                Aws => Aws,
                Gcp => Gcp,
                _ => throw new InvalidOperationException(
                    $"Unsupported Storage:Provider '{configuredProvider}'. Supported values are 'aws' and 'gcp'.")
            };
        }

        // Cloud Run always takes precedence over the AWS region in appsettings.json,
        // which remains present for backward compatibility with the AWS deployment.
        if (!string.IsNullOrWhiteSpace(cloudRunService))
            return Gcp;

        if (!string.IsNullOrWhiteSpace(awsExecutionEnvironment)
            || !string.IsNullOrWhiteSpace(ecsMetadataUri)
            || !string.IsNullOrWhiteSpace(awsRegion))
            return Aws;

        throw new InvalidOperationException(
            "Cannot determine object-storage provider. Set Storage:Provider to 'aws' or 'gcp'.");
    }
}
