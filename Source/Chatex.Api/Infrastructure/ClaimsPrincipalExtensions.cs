using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chatex.Api.Infrastructure;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id from the access token's <c>sub</c> claim. Only call it on authorized endpoints.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidOperationException("The request has no authenticated user id.");
}
