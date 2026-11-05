using Chatex.Business.Auth;

namespace Chatex.Api.Infrastructure;

internal static class HttpContextExtensions
{
    /// <summary>Where the request came from. The IP is the client's once forwarded headers are applied.</summary>
    public static ClientInfo GetClientInfo(this HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        return new ClientInfo(
            string.IsNullOrEmpty(userAgent) ? null : userAgent,
            context.Connection.RemoteIpAddress?.ToString());
    }
}
