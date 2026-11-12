using System.Security.Claims;
using Chatex.Api.Infrastructure;
using Chatex.Business.Common;
using Chatex.Business.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chatex.Api.Endpoints;

/// <summary>The signed-in user's own account under <c>/api/me</c>.</summary>
internal static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/me").WithTags("Profile");

        group.MapGet("", GetAsync).WithName("GetMyProfile");
        group.MapPut("", UpdateAsync).WithName("UpdateMyProfile");
        group.MapPut("/password", ChangePasswordAsync).WithName("ChangePassword")
            .WithSummary("Changes the password and ends every session, this one included.");
        group.MapPut("/avatar", SetAvatarAsync).WithName("SetMyAvatar").AcceptsFileUpTo(AvatarLimits.MaxBytes);
        group.MapDelete("/avatar", RemoveAvatarAsync).WithName("RemoveMyAvatar");
    }

    private static async Task<Results<Ok<MyProfileDto>, ProblemHttpResult>> GetAsync(
        ClaimsPrincipal user, IProfileService profiles, CancellationToken cancellationToken) =>
        (await profiles.GetMyProfileAsync(user.GetUserId(), cancellationToken)).ToOk();

    private static async Task<Results<Ok<MyProfileDto>, ProblemHttpResult>> UpdateAsync(
        UpdateProfileCommand command, ClaimsPrincipal user, IProfileService profiles, CancellationToken cancellationToken) =>
        (await profiles.UpdateProfileAsync(user.GetUserId(), command, cancellationToken)).ToOk();

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePasswordAsync(
        ChangePasswordCommand command, ClaimsPrincipal user, IProfileService profiles, HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await profiles.ChangePasswordAsync(user.GetUserId(), command, cancellationToken);
        if (result.IsSuccess)
        {
            RefreshTokenCookie.Clear(response);
        }

        return result.ToNoContent();
    }

    private static async Task<Results<Ok<MyProfileDto>, ProblemHttpResult>> SetAvatarAsync(
        IFormFile file, ClaimsPrincipal user, IProfileService profiles, CancellationToken cancellationToken) =>
        (await profiles.SetAvatarAsync(user.GetUserId(), file.ToFileUpload(), cancellationToken)).ToOk();

    private static async Task<Results<Ok<MyProfileDto>, ProblemHttpResult>> RemoveAvatarAsync(
        ClaimsPrincipal user, IProfileService profiles, CancellationToken cancellationToken) =>
        (await profiles.RemoveAvatarAsync(user.GetUserId(), cancellationToken)).ToOk();
}
