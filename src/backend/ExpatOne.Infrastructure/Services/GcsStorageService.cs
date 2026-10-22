using System.Net;
using System.Net.Http;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

/// <summary>
/// Google Cloud Storage implementation of the shared document-storage abstraction.
/// Uses Application Default Credentials for object operations and IAM-backed V4
/// signed URLs; no service-account key file is required.
/// </summary>
public sealed class GcsStorageService : IStorageService
{
    private readonly StorageClient _storageClient;
    private readonly UrlSigner _urlSigner;
    private readonly string _bucketName;
    private readonly ILogger<GcsStorageService> _logger;

    public GcsStorageService(
        StorageClient storageClient,
        UrlSigner urlSigner,
        IConfiguration configuration,
        ILogger<GcsStorageService> logger)
    {
        _storageClient = storageClient;
        _urlSigner = urlSigner;
        _bucketName = configuration["Storage:GoogleCloud:BucketName"]
            ?? throw new InvalidOperationException(
                "Storage:GoogleCloud:BucketName is required when Storage:Provider is 'gcp'.");
        if (string.IsNullOrWhiteSpace(_bucketName))
            throw new InvalidOperationException(
                "Storage:GoogleCloud:BucketName is required when Storage:Provider is 'gcp'.");
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, string? folder = null)
    {
        var objectKey = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";
        await _storageClient.UploadObjectAsync(_bucketName, objectKey, contentType, fileStream);
        _logger.LogInformation("Uploaded object to Google Cloud Storage");
        return objectKey;
    }

    public async Task<Stream> DownloadFileAsync(string objectKey)
    {
        // The interface returns an owned stream. Buffer into a MemoryStream so the
        // StorageClient request can finish before returning to the caller.
        var output = new MemoryStream();
        try
        {
            await _storageClient.DownloadObjectAsync(_bucketName, objectKey, output);
            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    public async Task DeleteFileAsync(string objectKey)
    {
        try
        {
            await _storageClient.DeleteObjectAsync(_bucketName, objectKey);
            _logger.LogInformation("Deleted object from Google Cloud Storage");
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Match the document/version deletion behavior for already-missing objects.
            _logger.LogDebug("Google Cloud Storage object was already absent");
        }
    }

    public Task<string> GeneratePresignedUrlAsync(string objectKey, TimeSpan expiry)
    {
        ValidateExpiry(expiry);
        return Task.FromResult(_urlSigner.Sign(_bucketName, objectKey, expiry, HttpMethod.Get, SigningVersion.V4));
    }

    public Task<string> GeneratePresignedUrlUntilAsync(string objectKey, DateTime expiresAtUtc)
    {
        if (expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("Download expiry must be a future UTC instant.");

        // Keep a small safety margin so signing latency cannot extend a family-access deadline.
        var remaining = expiresAtUtc - DateTime.UtcNow - TimeSpan.FromSeconds(2);
        ValidateExpiry(remaining);
        return Task.FromResult(_urlSigner.Sign(_bucketName, objectKey, remaining, HttpMethod.Get, SigningVersion.V4));
    }

    public string GeneratePresignedUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        ValidateExpiry(expiry);
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));

        var contentHeaders = new Dictionary<string, IEnumerable<string>>
        {
            ["Content-Type"] = new[] { contentType }
        };
        var template = UrlSigner.RequestTemplate
            .FromBucket(_bucketName)
            .WithObjectName(objectKey)
            .WithHttpMethod(HttpMethod.Put)
            .WithContentHeaders(contentHeaders);

        return _urlSigner.Sign(template, UrlSigner.Options.FromDuration(expiry).WithSigningVersion(SigningVersion.V4));
    }

    private static void ValidateExpiry(TimeSpan expiry)
    {
        if (expiry <= TimeSpan.Zero || expiry > TimeSpan.FromDays(7))
            throw new ArgumentOutOfRangeException(
                nameof(expiry), "Signed URL expiry must be greater than zero and no more than seven days.");
    }
}
