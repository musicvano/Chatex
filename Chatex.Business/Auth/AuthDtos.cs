using Chatex.Business.Users;

namespace Chatex.Business.Auth;

public sealed record RegisterCommand(string UserName, string Email, string Password, string DisplayName);

/// <param name="Login">User name or email.</param>
public sealed record LoginCommand(string Login, string Password);

public sealed record RefreshCommand(string RefreshToken);

/// <summary>Where a request came from; stored with refresh tokens so users can review their sessions.</summary>
public sealed record ClientInfo(string? UserAgent, string? IpAddress)
{
    public static ClientInfo Unknown { get; } = new(null, null);
}

/// <summary>Tokens for a signed-in user. The client sends the access token on each request and the refresh token to renew it.</summary>
public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    MyProfileDto User);
