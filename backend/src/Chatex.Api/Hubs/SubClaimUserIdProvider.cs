using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Chatex.Api.Hubs;

/// <summary>
/// Makes <c>Context.UserIdentifier</c> and <c>Clients.User(id)</c> use the access token's <c>sub</c> claim.
/// The default provider reads NameIdentifier, which is absent because the JWT handler keeps the raw claim names.
/// </summary>
internal sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
