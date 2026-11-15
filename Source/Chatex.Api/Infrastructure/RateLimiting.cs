using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Chatex.Api.Infrastructure;

internal sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Requests a client IP may make to the auth endpoints per window.</summary>
    [Range(1, 1_000_000)]
    public int AuthPermitLimit { get; set; } = 20;

    [Range(1, 3600)]
    public int AuthWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Throttles the anonymous auth endpoints per client IP, so passwords cannot be guessed across many accounts and
/// accounts cannot be created in bulk. Behind Nginx the IP comes from <c>X-Forwarded-For</c> (UseForwardedHeaders).
/// </summary>
internal static class RateLimiting
{
    public const string AuthPolicy = "auth";
    public const string TooManyRequestsCode = "too_many_requests";

    public static IServiceCollection AddChatexRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = WriteRejectionAsync;
        });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, options) =>
            {
                var settings = options.Value;
                limiter.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                        QueueLimit = 0,
                    }));
            });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Too many requests. Try again later.",
                Extensions = { [ResultHttpExtensions.CodeExtension] = TooManyRequestsCode },
            },
        });
    }
}
