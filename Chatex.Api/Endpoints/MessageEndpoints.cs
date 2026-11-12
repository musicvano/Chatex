using System.Security.Claims;
using Chatex.Api.Infrastructure;
using Chatex.Business.Common;
using Chatex.Business.Messages;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chatex.Api.Endpoints;

internal static class MessageEndpoints
{
    public static void MapMessageEndpoints(this IEndpointRouteBuilder routes)
    {
        var history = routes.MapGroup("/chats/{chatId:guid}/messages").WithTags("Messages");
        history.MapGet("", GetHistoryAsync).WithName("GetMessages")
            .WithSummary("A page of history: newest first, `before` for older pages, `after` to catch up (oldest first).");
        history.MapPost("", SendAsync).WithName("SendMessage");

        var messages = routes.MapGroup("/messages").WithTags("Messages");
        messages.MapPut("/{id:guid}", EditAsync).WithName("EditMessage").WithSummary("Only the sender can edit.");
        messages.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteMessage")
            .WithSummary("The sender or a chat admin can delete; a placeholder stays.");
    }

    private static async Task<Results<Ok<PageDto<MessageDto>>, ProblemHttpResult>> GetHistoryAsync(
        Guid chatId, string? before, string? after, int? limit, ClaimsPrincipal user, IMessageService messages,
        CancellationToken cancellationToken) =>
        (await messages.GetHistoryAsync(user.GetUserId(), chatId, before, after, limit, cancellationToken)).ToOk();

    private static async Task<Results<Created<MessageDto>, ProblemHttpResult>> SendAsync(
        Guid chatId, SendMessageCommand command, ClaimsPrincipal user, IMessageService messages,
        CancellationToken cancellationToken) =>
        (await messages.SendAsync(user.GetUserId(), chatId, command, cancellationToken)).ToCreated(_ => null);

    private static async Task<Results<Ok<MessageDto>, ProblemHttpResult>> EditAsync(
        Guid id, EditMessageCommand command, ClaimsPrincipal user, IMessageService messages, CancellationToken cancellationToken) =>
        (await messages.EditAsync(user.GetUserId(), id, command, cancellationToken)).ToOk();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id, ClaimsPrincipal user, IMessageService messages, CancellationToken cancellationToken) =>
        (await messages.DeleteAsync(user.GetUserId(), id, cancellationToken)).ToNoContent();
}
