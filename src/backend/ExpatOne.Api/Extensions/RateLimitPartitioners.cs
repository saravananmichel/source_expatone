using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Extensions;

// Partitions by the authenticated user's firebase_uid claim (internal to the server-side
// auth context — never supplied by the client). Falls back to remote IP for unauthenticated
// requests so unauthenticated abuse is also throttled.

public sealed class AiRateLimitPartitioner : IRateLimiterPolicy<string>
{
    private readonly RateLimitPartition<string> _noLimitPartition =
        RateLimitPartition.GetNoLimiter("no-limit");

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var uid = httpContext.User.FindFirstValue("firebase_uid");
        var key = string.IsNullOrEmpty(uid)
            ? $"ip:{httpContext.Connection.RemoteIpAddress}"
            : $"user:{uid}";

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = httpContext.RequestServices
                .GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:AiEndpoints:PermitLimit", 20),
            Window = TimeSpan.FromSeconds(httpContext.RequestServices
                .GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:AiEndpoints:WindowSeconds", 60)),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;
}

public sealed class EmergencyAiRateLimitPartitioner : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var uid = httpContext.User.FindFirstValue("firebase_uid");
        var key = string.IsNullOrEmpty(uid)
            ? $"ip:{httpContext.Connection.RemoteIpAddress}"
            : $"user:{uid}";

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = httpContext.RequestServices
                .GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:EmergencyAi:PermitLimit", 10),
            Window = TimeSpan.FromSeconds(httpContext.RequestServices
                .GetRequiredService<IConfiguration>()
                .GetValue("RateLimiting:EmergencyAi:WindowSeconds", 60)),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;
}
