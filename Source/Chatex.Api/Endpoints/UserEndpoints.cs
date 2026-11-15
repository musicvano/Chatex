using Chatex.Api.Infrastructure;
using Chatex.Business.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chatex.Api.Endpoints;

internal static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/users").WithTags("Users");

        group.MapGet("", SearchAsync).WithName("SearchUsers")
            .WithSummary("Finds users by user name or display name (at least 2 characters).");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetUser");
        group.MapGet("/{id:guid}/avatar", GetAvatarAsync).WithName("GetUserAvatar");
    }

    private static async Task<Results<Ok<UserSummaryDto[]>, ProblemHttpResult>> SearchAsync(
        string? query, int? limit, IProfileService profiles, CancellationToken cancellationToken) =>
        (await profiles.SearchUsersAsync(query ?? "", limit, cancellationToken)).ToOk();

    private static async Task<Results<Ok<UserDto>, ProblemHttpResult>> GetAsync(
        Guid id, IProfileService profiles, CancellationToken cancellationToken) =>
        (await profiles.GetUserAsync(id, cancellationToken)).ToOk();

    private static Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetAvatarAsync(
        Guid id, IProfileService profiles, HttpResponse response, CancellationToken cancellationToken) =>
        profiles.OpenAvatarAsync(id, cancellationToken).ToImageAsync(response, cancellationToken);
}
