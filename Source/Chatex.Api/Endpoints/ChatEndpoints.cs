using System.Security.Claims;
using Chatex.Api.Contracts;
using Chatex.Api.Infrastructure;
using Chatex.Business.Chats;
using Chatex.Business.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chatex.Api.Endpoints;

internal static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/chats").WithTags("Chats");

        group.MapGet("", GetMyChatsAsync).WithName("GetMyChats")
            .WithSummary("The caller's chats, most recently active first.");
        group.MapPost("", CreateAsync).WithName("CreateChat").WithSummary("Creates a group or channel.");
        group.MapPost("/direct", OpenDirectAsync).WithName("OpenDirectChat")
            .WithSummary("Returns the direct chat with a user, creating it on first use.");
        group.MapGet("/search", SearchAsync).WithName("SearchPublicChats");

        group.MapGet("/{id:guid}", GetAsync).WithName("GetChat");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateChat");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteChat");

        group.MapGet("/{id:guid}/avatar", GetAvatarAsync).WithName("GetChatAvatar");
        group.MapPut("/{id:guid}/avatar", SetAvatarAsync).WithName("SetChatAvatar").AcceptsFileUpTo(AvatarLimits.MaxBytes);
        group.MapDelete("/{id:guid}/avatar", RemoveAvatarAsync).WithName("RemoveChatAvatar");

        group.MapPost("/{id:guid}/join", JoinAsync).WithName("JoinChat");
        group.MapPost("/{id:guid}/leave", LeaveAsync).WithName("LeaveChat");

        group.MapGet("/{id:guid}/members", GetMembersAsync).WithName("GetChatMembers");
        group.MapPost("/{id:guid}/members", AddMembersAsync).WithName("AddChatMembers");
        group.MapDelete("/{id:guid}/members/{userId:guid}", RemoveMemberAsync).WithName("RemoveChatMember");
        group.MapPut("/{id:guid}/members/{userId:guid}/role", ChangeRoleAsync).WithName("ChangeChatMemberRole")
            .WithSummary("Owner only. Making someone the owner transfers ownership.");

        group.MapPut("/{id:guid}/mute", SetMutedAsync).WithName("SetChatMuted");
        group.MapPost("/{id:guid}/read", MarkReadAsync).WithName("MarkChatRead")
            .WithSummary("Moves the caller's read position forward to a message.");
    }

    private static async Task<Results<Ok<PageDto<ChatListItemDto>>, ProblemHttpResult>> GetMyChatsAsync(
        string? cursor, int? limit, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.GetMyChatsAsync(user.GetUserId(), cursor, limit, cancellationToken)).ToOk();

    private static async Task<Results<Created<ChatDto>, ProblemHttpResult>> CreateAsync(
        CreateChatCommand command, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.CreateAsync(user.GetUserId(), command, cancellationToken)).ToCreated(c => $"/api/chats/{c.Id}");

    private static async Task<Results<Ok<ChatDto>, ProblemHttpResult>> OpenDirectAsync(
        OpenDirectRequest request, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.OpenDirectAsync(user.GetUserId(), request.UserId, cancellationToken)).ToOk();

    private static async Task<Results<Ok<ChatSummaryDto[]>, ProblemHttpResult>> SearchAsync(
        string? query, int? limit, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.SearchPublicAsync(query ?? "", limit, cancellationToken)).ToOk();

    private static async Task<Results<Ok<ChatDto>, ProblemHttpResult>> GetAsync(
        Guid id, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.GetAsync(user.GetUserId(), id, cancellationToken)).ToOk();

    private static async Task<Results<Ok<ChatDto>, ProblemHttpResult>> UpdateAsync(
        Guid id, UpdateChatCommand command, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.UpdateAsync(user.GetUserId(), id, command, cancellationToken)).ToOk();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.DeleteAsync(user.GetUserId(), id, cancellationToken)).ToNoContent();

    private static Task<Results<FileStreamHttpResult, ProblemHttpResult>> GetAvatarAsync(
        Guid id, ClaimsPrincipal user, IChatService chats, HttpResponse response, CancellationToken cancellationToken) =>
        chats.OpenAvatarAsync(user.GetUserId(), id, cancellationToken).ToImageAsync(response, cancellationToken);

    private static async Task<Results<Ok<ChatDto>, ProblemHttpResult>> SetAvatarAsync(
        Guid id, IFormFile file, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.SetAvatarAsync(user.GetUserId(), id, file.ToFileUpload(), cancellationToken)).ToOk();

    private static async Task<Results<Ok<ChatDto>, ProblemHttpResult>> RemoveAvatarAsync(
        Guid id, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.RemoveAvatarAsync(user.GetUserId(), id, cancellationToken)).ToOk();

    private static async Task<Results<Ok<ChatDto>, ProblemHttpResult>> JoinAsync(
        Guid id, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.JoinAsync(user.GetUserId(), id, cancellationToken)).ToOk();

    private static async Task<Results<NoContent, ProblemHttpResult>> LeaveAsync(
        Guid id, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.LeaveAsync(user.GetUserId(), id, cancellationToken)).ToNoContent();

    private static async Task<Results<Ok<PageDto<ChatMemberDto>>, ProblemHttpResult>> GetMembersAsync(
        Guid id, string? cursor, int? limit, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.GetMembersAsync(user.GetUserId(), id, cursor, limit, cancellationToken)).ToOk();

    private static async Task<Results<Ok<AddMembersResponse>, ProblemHttpResult>> AddMembersAsync(
        Guid id, AddMembersRequest request, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken)
    {
        var result = await chats.AddMembersAsync(user.GetUserId(), id, request.UserIds ?? [], cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(new AddMembersResponse(result.Value)) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveMemberAsync(
        Guid id, Guid userId, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.RemoveMemberAsync(user.GetUserId(), id, userId, cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeRoleAsync(
        Guid id, Guid userId, ChangeRoleRequest request, ClaimsPrincipal user, IChatService chats,
        CancellationToken cancellationToken) =>
        (await chats.ChangeRoleAsync(user.GetUserId(), id, userId, request.Role, cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> SetMutedAsync(
        Guid id, SetMutedRequest request, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.SetMutedAsync(user.GetUserId(), id, request.Muted, cancellationToken)).ToNoContent();

    private static async Task<Results<NoContent, ProblemHttpResult>> MarkReadAsync(
        Guid id, MarkReadRequest request, ClaimsPrincipal user, IChatService chats, CancellationToken cancellationToken) =>
        (await chats.MarkReadAsync(user.GetUserId(), id, request.MessageId, cancellationToken)).ToNoContent();
}
