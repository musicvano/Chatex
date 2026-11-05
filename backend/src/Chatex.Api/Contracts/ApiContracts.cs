using Chatex.Business.Auth;
using Chatex.Business.Users;
using Chatex.DataAccess.Entities;

namespace Chatex.Api.Contracts;

/// <summary>
/// A signed-in session as the client sees it. The refresh token is not here: it travels in the
/// <c>chatex_refresh</c> HttpOnly cookie, which only <c>/api/auth</c> receives.
/// </summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, MyProfileDto User)
{
    public static AuthResponse From(AuthResult auth) => new(auth.AccessToken, auth.AccessTokenExpiresAt, auth.User);
}

public sealed record OpenDirectRequest(Guid UserId);

public sealed record AddMembersRequest(IReadOnlyList<Guid>? UserIds);

/// <param name="Added">How many of the users were not members yet.</param>
public sealed record AddMembersResponse(int Added);

public sealed record ChangeRoleRequest(ChatMemberRole Role);

public sealed record SetMutedRequest(bool Muted);

public sealed record MarkReadRequest(Guid MessageId);
