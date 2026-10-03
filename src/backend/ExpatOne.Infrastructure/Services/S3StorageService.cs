using Amazon.S3;
using Amazon.S3.Model;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _bucketName = configuration["Aws:S3BucketName"]
            ?? throw new InvalidOperationException("Aws:S3BucketName is not configured.");
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null)
    {
        var objectKey = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = fileStream,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };

        await _s3Client.PutObjectAsync(request);
        _logger.LogInformation("Uploaded object to private storage");
        return objectKey;
    }

    public async Task<Stream> DownloadFileAsync(string objectKey)
    {
        var response = await _s3Client.GetObjectAsync(_bucketName, objectKey);
        return response.ResponseStream;
    }

    public async Task DeleteFileAsync(string objectKey)
    {
        await _s3Client.DeleteObjectAsync(_bucketName, objectKey);
        _logger.LogInformation("Deleted object from private storage");
    }

    public Task<string> GeneratePresignedUrlAsync(string objectKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public string GeneratePresignedUploadUrl(string objectKey, string contentType, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
