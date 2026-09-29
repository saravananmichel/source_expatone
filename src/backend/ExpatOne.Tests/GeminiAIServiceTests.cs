using System.Net;
using System.Text;
using System.Text.Json;
using ExpatOne.Application.Common;
using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class GeminiAIServiceTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestModel = "gemini-3.8-flash";

    private static readonly string ValidGeminiResponse = JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new
            {
                content = new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = JsonSerializer.Serialize(new
                            {
                                documentCategory = "Passport",
                                summary = "A travel document",
                                importantDates = Array.Empty<object>(),
                                keyInformation = Array.Empty<object>(),
                                warnings = Array.Empty<string>(),
                                terminology = Array.Empty<object>(),
                            })
                        }
                    }
                }
            }
        }
    });

    private static readonly string ValidAssistantResponse = JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new
            {
                content = new
                {
                    parts = new[]
                    {
                        new { text = "The minimum salary for Category II is RM10,000." }
                    }
                }
            }
        }
    });

    private static HttpResponseMessage MakeOkAssistantResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidAssistantResponse, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage MakeErrorResponse(HttpStatusCode statusCode) =>
        new(statusCode)
        {
            Content = new StringContent($"{{\"error\":\"test {statusCode}\"}}")
        };

    private GeminiAIService CreateServiceWithHandler(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = TestApiKey,
                ["Gemini:Model"] = TestModel,
                ["Gemini:EmbeddingModel"] = "gemini-embedding-2",
            })
            .Build();

        var logger = new Mock<ILogger<GeminiAIService>>().Object;
        return new GeminiAIService(httpClient, config, logger);
    }

    private (GeminiAIService service, MockHttpMessageHandler handler) CreateService(
        HttpResponseMessage? response = null)
    {
        var handler = new MockHttpMessageHandler(response ?? new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = TestApiKey,
                ["Gemini:Model"] = TestModel,
                ["Gemini:EmbeddingModel"] = "gemini-embedding-2",
            })
            .Build();

        var logger = new Mock<ILogger<GeminiAIService>>().Object;
        var service = new GeminiAIService(httpClient, config, logger);

        return (service, handler);
    }

    [Fact]
    public async Task AnalyzeDocument_SendsCorrectEndpoint()
    {
        var (svc, handler) = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        await svc.AnalyzeDocumentAsync(stream, "application/pdf");

        Assert.NotNull(handler.LastRequest);
        var url = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains($"models/{TestModel}:generateContent", url);
        Assert.DoesNotContain("key=", url);
        Assert.True(handler.LastRequest.Headers.Contains("x-goog-api-key"), "API key must be in header, not URL");
        Assert.Equal(TestApiKey, handler.LastRequest.Headers.GetValues("x-goog-api-key").First());
    }

    [Fact]
    public async Task AnalyzeDocument_SendsCorrectMimeType()
    {
        var (svc, handler) = CreateService();
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });

        await svc.AnalyzeDocumentAsync(stream, "image/jpeg");

        var requestBody = handler.LastRequestBody!;
        Assert.Contains("image/jpeg", requestBody);
    }

    [Fact]
    public async Task AnalyzeDocument_SendsBase64Content()
    {
        var (svc, handler) = CreateService();
        var testBytes = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testBytes);
        var expectedBase64 = Convert.ToBase64String(testBytes);

        await svc.AnalyzeDocumentAsync(stream, "application/pdf");

        Assert.Contains(expectedBase64, handler.LastRequestBody!);
    }

    [Fact]
    public async Task AnalyzeDocument_IncludesStructuredJsonConfig()
    {
        var (svc, handler) = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x25 });

        await svc.AnalyzeDocumentAsync(stream, "application/pdf");

        Assert.Contains("application/json", handler.LastRequestBody!);
        Assert.Contains("response_schema", handler.LastRequestBody!);
    }

    [Fact]
    public async Task AnalyzeDocument_ParsesSuccessfulResponse()
    {
        var (svc, _) = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x25 });

        var result = await svc.AnalyzeDocumentAsync(stream, "application/pdf");

        Assert.NotNull(result);
        Assert.NotNull(result.StructuredJson);
        Assert.Contains("Passport", result.StructuredJson);
    }

    [Fact]
    public async Task AnalyzeDocument_RejectsUnsupportedContentType()
    {
        var (svc, _) = CreateService();
        using var stream = new MemoryStream(new byte[] { 0x25 });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/zip"));
    }

    [Fact]
    public async Task AnalyzeDocument_Handles400()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"bad request\"}")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));

        Assert.Contains("could not be processed", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocument_Handles401()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"unauthorized\"}")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));

        Assert.Contains("configuration error", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocument_Handles403()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":\"forbidden\"}")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));

        Assert.Contains("configuration error", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocument_Handles429()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":\"rate limited\"}")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        var ex = await Assert.ThrowsAsync<AIProviderUnavailableException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));

        Assert.Contains("temporarily unavailable", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocument_Handles500()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"server error\"}")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        var ex = await Assert.ThrowsAsync<AIProviderUnavailableException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));

        Assert.Contains("temporarily unavailable", ex.Message);
    }

    [Fact]
    public async Task AnalyzeDocument_HandlesMalformedResponse_NoCandidates()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"candidates\":[]}", Encoding.UTF8, "application/json")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));
    }

    [Fact]
    public async Task AnalyzeDocument_HandlesMalformedResponse_NoContent()
    {
        var response = JsonSerializer.Serialize(new
        {
            candidates = new[] { new { finishReason = "STOP" } }
        });
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        });
        using var stream = new MemoryStream(new byte[] { 0x25 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));
    }

    [Fact]
    public async Task AnalyzeDocument_HandlesTimeout()
    {
        var handler = new TimeoutHttpMessageHandler();
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(1) };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = TestApiKey,
                ["Gemini:Model"] = TestModel,
            })
            .Build();

        var logger = new Mock<ILogger<GeminiAIService>>().Object;
        var svc = new GeminiAIService(httpClient, config, logger);

        using var stream = new MemoryStream(new byte[] { 0x25 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(stream, "application/pdf"));

        Assert.Contains("timed out", ex.Message);
    }

    [Fact]
    public void Translate_ThrowsNotImplemented()
    {
        var (svc, _) = CreateService();

        Assert.ThrowsAsync<NotImplementedException>(() =>
            svc.TranslateAsync("hello", "en", "ms"));
    }

    [Fact]
    public async Task GenerateEmbedding_SendsCorrectEndpoint()
    {
        var embeddingResponse = BuildEmbeddingResponse(768);
        var (svc, handler) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(embeddingResponse, Encoding.UTF8, "application/json")
        });

        await svc.GenerateEmbeddingAsync("test text");

        Assert.NotNull(handler.LastRequest);
        var url = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("gemini-embedding-2:embedContent", url);
        Assert.DoesNotContain("key=", url);
        Assert.True(handler.LastRequest.Headers.Contains("x-goog-api-key"), "API key must be in header, not URL");
    }

    [Fact]
    public async Task GenerateEmbedding_SendsCorrectModelAndDimensionality()
    {
        var embeddingResponse = BuildEmbeddingResponse(768);
        var (svc, handler) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(embeddingResponse, Encoding.UTF8, "application/json")
        });

        await svc.GenerateEmbeddingAsync("test text");

        Assert.Contains("models/gemini-embedding-2", handler.LastRequestBody!);
        Assert.Contains("\"output_dimensionality\":768", handler.LastRequestBody!);
    }

    [Fact]
    public async Task GenerateEmbedding_Returns768Floats()
    {
        var embeddingResponse = BuildEmbeddingResponse(768);
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(embeddingResponse, Encoding.UTF8, "application/json")
        });

        var result = await svc.GenerateEmbeddingAsync("test text");

        Assert.Equal(768, result.Length);
    }

    [Fact]
    public async Task GenerateEmbedding_WrongDimensionality_Throws()
    {
        var embeddingResponse = BuildEmbeddingResponse(512);
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(embeddingResponse, Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerateEmbeddingAsync("test text"));
    }

    [Fact]
    public async Task GenerateEmbedding_EmptyText_Rejected()
    {
        var (svc, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GenerateEmbeddingAsync(""));
    }

    [Fact]
    public async Task GenerateEmbedding_Handles429()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":\"rate limited\"}")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerateEmbeddingAsync("test"));

        Assert.Contains("rate limiting", ex.Message);
    }

    [Fact]
    public async Task GenerateEmbedding_Handles500()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"server error\"}")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerateEmbeddingAsync("test"));

        Assert.Contains("temporarily unavailable", ex.Message);
    }

    [Fact]
    public async Task GenerateEmbedding_MalformedResponse_Throws()
    {
        var (svc, _) = CreateService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"wrong\":\"format\"}", Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerateEmbeddingAsync("test"));
    }

    [Fact]
    public async Task GenerateResponse_Transient503_IsRetried()
    {
        var handler = new SequentialHttpMessageHandler(
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable),
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable),
            MakeOkAssistantResponse());

        var svc = CreateServiceWithHandler(handler);
        var result = await svc.GenerateResponseAsync("test prompt");

        Assert.Equal(3, handler.CallCount);
        Assert.Contains("RM10,000", result.Content);
    }

    [Fact]
    public async Task GenerateResponse_Transient429_IsRetried()
    {
        var handler = new SequentialHttpMessageHandler(
            MakeErrorResponse(HttpStatusCode.TooManyRequests),
            MakeOkAssistantResponse());

        var svc = CreateServiceWithHandler(handler);
        var result = await svc.GenerateResponseAsync("test prompt");

        Assert.Equal(2, handler.CallCount);
        Assert.Contains("RM10,000", result.Content);
    }

    [Fact]
    public async Task GenerateResponse_RetriesEventuallySucceed()
    {
        var handler = new SequentialHttpMessageHandler(
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable),
            MakeErrorResponse(HttpStatusCode.TooManyRequests),
            MakeErrorResponse(HttpStatusCode.InternalServerError),
            MakeOkAssistantResponse());

        var svc = CreateServiceWithHandler(handler);
        var result = await svc.GenerateResponseAsync("test prompt");

        Assert.Equal(4, handler.CallCount);
        Assert.Contains("RM10,000", result.Content);
    }

    [Fact]
    public async Task GenerateResponse_RetriesExhausted_ThrowsAIProviderUnavailable()
    {
        var handler = new SequentialHttpMessageHandler(
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable),
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable),
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable),
            MakeErrorResponse(HttpStatusCode.ServiceUnavailable));

        var svc = CreateServiceWithHandler(handler);

        var ex = await Assert.ThrowsAsync<AIProviderUnavailableException>(
            () => svc.GenerateResponseAsync("test prompt"));

        Assert.Equal(4, handler.CallCount);
        Assert.Contains("temporarily unavailable", ex.Message);
    }

    [Fact]
    public async Task GenerateResponse_Permanent400_NotRetried()
    {
        var handler = new SequentialHttpMessageHandler(
            MakeErrorResponse(HttpStatusCode.BadRequest));

        var svc = CreateServiceWithHandler(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.GenerateResponseAsync("test prompt"));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateResponse_Permanent401_NotRetried()
    {
        var handler = new SequentialHttpMessageHandler(
            MakeErrorResponse(HttpStatusCode.Unauthorized));

        var svc = CreateServiceWithHandler(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GenerateResponseAsync("test prompt"));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateResponse_SuccessfulResponse_NoRetry()
    {
        var handler = new SequentialHttpMessageHandler(MakeOkAssistantResponse());

        var svc = CreateServiceWithHandler(handler);
        var result = await svc.GenerateResponseAsync("test prompt");

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("RM10,000", result.Content);
    }

    private static string BuildEmbeddingResponse(int dimensions)
    {
        var values = string.Join(",", Enumerable.Range(0, dimensions).Select(i => "0.01"));
        return $"{{\"embedding\":{{\"values\":[{values}]}}}}";
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public int CallCount { get; private set; }

    public MockHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (request.Content is not null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        return _response;
    }
}

public class SequentialHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;
    public int CallCount { get; private set; }

    public SequentialHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (_responses.Count == 0)
            throw new InvalidOperationException("No more responses configured.");
        return Task.FromResult(_responses.Dequeue());
    }
}

public class TimeoutHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
