using System.Net;
using System.Text.Json;
using ExpatOne.Application.Common;

namespace ExpatOne.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unhandled exception category={Category}", ex.GetType().Name);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Authentication required."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, exception.Message),
            AIProviderUnavailableException => (HttpStatusCode.ServiceUnavailable, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "We couldn't process your request right now. Please try again.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new ApiErrorResponse { Message = message };
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
