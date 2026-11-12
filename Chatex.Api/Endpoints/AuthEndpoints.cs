using System.Security.Claims;
using Chatex.Api.Contracts;
using Chatex.Api.Infrastructure;
using Chatex.Business.Auth;
using Chatex.Business.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chatex.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).AllowAnonymous().WithName("Register")
            .WithSummary("Creates an account and signs it in.");
        group.MapPost("/login", LoginAsync).AllowAnonymous().WithName("Login")
            .WithSummary("Signs in with a user name or email.");
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous().WithName("Refresh")
            .WithSummary("Swaps the refresh token cookie for a new access token and cookie.");
        group.MapPost("/logout", LogoutAsync).AllowAnonymous().WithName("Logout")
            .WithSummary("Ends the session of the refresh token cookie.");
        group.MapPost("/logout-all", LogoutAllAsync).WithName("LogoutAll")
            .WithSummary("Ends every session of the signed-in user.");
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterCommand command, IAuthService auth, HttpContext context, CancellationToken cancellationToken) =>
        SignIn(await auth.RegisterAsync(command, context.GetClientInfo(), cancellationToken), context.Response);

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> LoginAsync(
        LoginCommand command, IAuthService auth, HttpContext context, CancellationToken cancellationToken) =>
        SignIn(await auth.LoginAsync(command, context.GetClientInfo(), cancellationToken), context.Response);

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> RefreshAsync(
        IAuthService auth, HttpContext context, CancellationToken cancellationToken)
    {
        var token = RefreshTokenCookie.Read(context.Request);
        var result = token is null
            ? AuthErrors.InvalidRefreshToken
            : await auth.RefreshAsync(new RefreshCommand(token), context.GetClientInfo(), cancellationToken);

        if (!result.IsSuccess)
        {
            RefreshTokenCookie.Clear(context.Response);
        }

        return SignIn(result, context.Response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LogoutAsync(
        IAuthService auth, HttpContext context, CancellationToken cancellationToken)
    {
        var token = RefreshTokenCookie.Read(context.Request);
        var result = token is null ? Result.Success : await auth.LogoutAsync(new RefreshCommand(token), cancellationToken);
        RefreshTokenCookie.Clear(context.Response);
        return result.ToNoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LogoutAllAsync(
        ClaimsPrincipal user, IAuthService auth, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await auth.LogoutAllAsync(user.GetUserId(), cancellationToken);
        if (result.IsSuccess)
        {
            RefreshTokenCookie.Clear(response);
        }

        return result.ToNoContent();
    }

    private static Results<Ok<AuthResponse>, ProblemHttpResult> SignIn(Result<AuthResult> result, HttpResponse response)
    {
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        RefreshTokenCookie.Set(response, result.Value);
        return TypedResults.Ok(AuthResponse.From(result.Value));
    }
}
