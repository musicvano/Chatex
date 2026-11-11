using Chatex.Business.Abstractions;
using Chatex.Business.Messages;
using Microsoft.AspNetCore.SignalR;

namespace Chatex.Api.Hubs;

/// <summary>Pushes the business layer's chat events to the chat's SignalR group.</summary>
internal sealed class SignalRChatNotifier(IHubContext<ChatHub, IChatClient> hub, PresenceTracker tracker) : IChatNotifier
{
    public Task MessageCreatedAsync(MessageDto message, CancellationToken cancellationToken = default) =>
        Chat(message.ChatId).MessageCreated(message);

    public Task MessageUpdatedAsync(MessageDto message, CancellationToken cancellationToken = default) =>
        Chat(message.ChatId).MessageUpdated(message);

    public Task MessageDeletedAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default) =>
        Chat(chatId).MessageDeleted(chatId, messageId);

    public Task MessagesReadAsync(Guid chatId, Guid userId, Guid lastReadMessageId, CancellationToken cancellationToken = default) =>
        Chat(chatId).MessagesRead(chatId, userId, lastReadMessageId);

    public async Task MembersAddedAsync(Guid chatId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        // Join the new members' open connections first, so they get this event and everything after it without reconnecting.
        var group = ChatHub.GroupName(chatId);
        foreach (var userId in userIds)
        {
            foreach (var connectionId in tracker.GetConnections(userId))
            {
                await hub.Groups.AddToGroupAsync(connectionId, group, cancellationToken);
            }
        }

        await hub.Clients.Group(group).MembersAdded(chatId, [.. userIds]);
    }

    public async Task MemberRemovedAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
    {
        // Send first, so the removed user learns about it too, then stop their events from this chat.
        var group = ChatHub.GroupName(chatId);
        await hub.Clients.Group(group).MemberRemoved(chatId, userId);

        foreach (var connectionId in tracker.GetConnections(userId))
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
        }
    }

    public Task ChatUpdatedAsync(Guid chatId, CancellationToken cancellationToken = default) =>
        Chat(chatId).ChatUpdated(chatId);

    public Task ChatDeletedAsync(Guid chatId, CancellationToken cancellationToken = default) =>
        Chat(chatId).ChatDeleted(chatId);

    private IChatClient Chat(Guid chatId) => hub.Clients.Group(ChatHub.GroupName(chatId));
}
