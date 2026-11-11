using Chatex.Api.Infrastructure;
using Chatex.Business.Chats;
using Chatex.Business.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Chatex.Api.Hubs;

/// <summary>
/// The realtime endpoint at <c>/hubs/chat</c>. Each connection joins a group per chat of its user; services push
/// events through <see cref="SignalRChatNotifier"/>, and the hub itself handles presence and typing.
/// </summary>
[Authorize]
internal sealed class ChatHub(
    IChatService chats,
    IProfileService profiles,
    PresenceTracker tracker,
    PresenceService presence) : Hub<IChatClient>
{
    public static string GroupName(Guid chatId) => $"chat:{chatId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        var cancellationToken = Context.ConnectionAborted;

        // Register before reading the chat list: a member added in between then finds this connection in the
        // tracker (SignalRChatNotifier adds it to the group), and one added earlier is already in the list.
        var first = tracker.Add(userId, Context.ConnectionId);

        var chatIds = await chats.GetMyChatIdsAsync(userId, cancellationToken);
        foreach (var chatId in chatIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(chatId), cancellationToken);
        }

        if (first)
        {
            await profiles.TouchLastSeenAsync(userId, cancellationToken);
            if (chatIds.Count > 0)
            {
                await Clients.Groups(chatIds.Select(GroupName).ToList()).UserOnline(userId);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User!.GetUserId();
        if (tracker.Remove(userId, Context.ConnectionId))
        {
            presence.ScheduleOffline(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Tells the other members that the caller is typing. Clients call it at most once every 3 seconds.</summary>
    public async Task Typing(Guid chatId)
    {
        var userId = Context.User!.GetUserId();
        if (!await chats.IsMemberAsync(chatId, userId, Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of this chat.");
        }

        await Clients.OthersInGroup(GroupName(chatId)).Typing(chatId, userId);
    }

    /// <summary>Returns which of <paramref name="userIds"/> have at least one open connection.</summary>
    public Guid[] GetOnlineUsers(Guid[] userIds) =>
        (userIds ?? []).Distinct().Where(tracker.IsOnline).ToArray();
}
