using Chatex.Business.Auth;

namespace Chatex.Api.Infrastructure;

/// <summary>
/// The refresh token lives in an HttpOnly cookie that only the auth endpoints receive, so page scripts never see it.
/// SameSite=Strict keeps other sites from sending it along.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "chatex_refresh";
    public const string Path = "/api/auth";

    public static void Set(HttpResponse response, AuthResult auth) =>
        response.Cookies.Append(Name, auth.RefreshToken, Options(auth.RefreshTokenExpiresAt));

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrEmpty(token) ? token : null;

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options(expires: null));

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
