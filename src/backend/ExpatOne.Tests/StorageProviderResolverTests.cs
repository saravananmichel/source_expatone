using ExpatOne.Infrastructure.Services;

namespace ExpatOne.Tests;

public class StorageProviderResolverTests
{
    [Theory]
    [InlineData("aws", "aws")]
    [InlineData("GCP", "gcp")]
    public void Explicit_configuration_takes_precedence(string configured, string expected)
    {
        var actual = StorageProviderResolver.Resolve(
            configured, "cloud-run-service", "AWS_ECS_FARGATE", "http://ecs-metadata", "ap-southeast-5");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Cloud_Run_marker_wins_over_AWS_defaults_in_appsettings()
    {
        var actual = StorageProviderResolver.Resolve(
            null, "expatone-api", null, null, "ap-southeast-5");
        Assert.Equal(StorageProviderResolver.Gcp, actual);
    }

    [Theory]
    [InlineData("AWS_ECS_FARGATE", null, "ap-southeast-5")]
    [InlineData(null, "http://169.254.170.2/v3", null)]
    [InlineData(null, null, "ap-southeast-5")]
    public void AWS_markers_select_AWS(
        string? awsExecutionEnvironment, string? ecsMetadataUri, string? awsRegion)
    {
        var actual = StorageProviderResolver.Resolve(
            null, null, awsExecutionEnvironment, ecsMetadataUri, awsRegion);
        Assert.Equal(StorageProviderResolver.Aws, actual);
    }

    [Fact]
    public void Unsupported_explicit_provider_fails_clearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            StorageProviderResolver.Resolve("azure", null, null, null, null));
        Assert.Contains("Supported values are 'aws' and 'gcp'", exception.Message);
    }

    [Fact]
    public void Missing_configuration_and_cloud_markers_fails_clearly()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StorageProviderResolver.Resolve(null, null, null, null, null));
    }
}
