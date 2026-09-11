namespace ExpatOne.Application.Interfaces;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null);
    Task<Stream> DownloadFileAsync(string objectKey);
    Task DeleteFileAsync(string objectKey);
    Task<string> GeneratePresignedUrlAsync(string objectKey, TimeSpan expiry);
    string GeneratePresignedUploadUrl(string objectKey, string contentType, TimeSpan expiry);
}
