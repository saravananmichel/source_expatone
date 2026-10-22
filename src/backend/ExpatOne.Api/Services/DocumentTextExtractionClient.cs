using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ExpatOne.Api.Services;

public sealed class DocumentTextExtractionClient(
    HttpClient http,
    IConfiguration configuration)
{
    public async Task<string> ExtractAsync(
        byte[] data,
        string contentType,
        CancellationToken cancellationToken)
    {
        var key = configuration["DocumentTextExtraction:ServiceKey"];
        if (string.IsNullOrWhiteSpace(key))
            key = configuration["DocumentIntelligence:ServiceKey"];

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "Document text extraction service key is not configured.");

        using var multipart = new MultipartFormDataContent();
        using var file = new ByteArrayContent(data);

        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", "document");

        // Buffer the complete multipart body so the HTTP request has a known
        // Content-Length when sent through the Cloud Run HTTPS endpoint.
        var multipartContentType = multipart.Headers.ContentType?.ToString()
            ?? throw new InvalidOperationException(
                "Multipart extraction request has no content type.");

        var body = await multipart.ReadAsByteArrayAsync(cancellationToken);
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType =
            MediaTypeHeaderValue.Parse(multipartContentType);
        content.Headers.ContentLength = body.Length;

        using var request = new HttpRequestMessage(HttpMethod.Post, "extract")
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("X-Service-Key", key);

        using var response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ExtractionResponse>(
            cancellationToken: cancellationToken);

        if (result?.ExtractedText is null)
            throw new InvalidDataException(
                "Extraction service returned no extracted text.");

        return result.ExtractedText;
    }

    private sealed record ExtractionResponse(
        string? ExtractedText,
        int PageCount);
}
