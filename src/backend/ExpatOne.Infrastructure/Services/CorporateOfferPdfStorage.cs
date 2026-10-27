using ExpatOne.Application.Interfaces;

namespace ExpatOne.Infrastructure.Services;

/// <summary>
/// Stores generated offer PDFs through the configured storage provider.
/// This class does not grant access or create wallet records; callers must
/// authorize the HR action and persist the employee-owned document.
/// </summary>
public sealed class CorporateOfferPdfStorage
{
    private readonly IStorageService _storage;

    public CorporateOfferPdfStorage(IStorageService storage)
    {
        _storage = storage;
    }

    public async Task<string> UploadAsync(
        Guid companyId,
        Guid employeeUserId,
        Guid documentId,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || employeeUserId == Guid.Empty ||
            documentId == Guid.Empty)
            throw new ArgumentException("Company, employee and document IDs are required.");
        if (pdf is null || pdf.Length < 5 || pdf.Length > 10 * 1024 * 1024 ||
            pdf[0] != 0x25 || pdf[1] != 0x50 || pdf[2] != 0x44 ||
            pdf[3] != 0x46 || pdf[4] != 0x2D)
            throw new ArgumentException("A valid PDF up to 10 MB is required.", nameof(pdf));

        cancellationToken.ThrowIfCancellationRequested();
        var folder = $"users/{employeeUserId}/documents/{documentId}/corporate-offers/{companyId}";
        await using var stream = new MemoryStream(pdf, writable: false);
        return await _storage.UploadFileAsync(
            stream, "employment-offer.pdf", "application/pdf", folder);
    }
}
