using Chatex.Business.Messages;

namespace Chatex.Api.Hubs;

/// <summary>
/// Events the server pushes to clients of <see cref="ChatHub"/>. Chat events go to the group <c>chat:{chatId}</c>,
/// so only current members receive them. The frontend is coded against these names and arguments.
/// </summary>
public interface IChatClient
{
    Task MessageCreated(MessageDto message);

    Task MessageUpdated(MessageDto message);

    Task MessageDeleted(Guid chatId, Guid messageId);

    /// <summary>A member's read position moved forward. Not sent for channels.</summary>
    Task MessagesRead(Guid chatId, Guid userId, Guid lastReadMessageId);

    /// <summary>Users joined or were added, including everyone in a newly created chat.</summary>
    Task MembersAdded(Guid chatId, Guid[] userIds);

    /// <summary>A user left or was removed. The removed user receives it too, then stops getting the chat's events.</summary>
    Task MemberRemoved(Guid chatId, Guid userId);

    /// <summary>Title, description, handle, avatar or member roles changed; refetch the chat.</summary>
    Task ChatUpdated(Guid chatId);

    Task ChatDeleted(Guid chatId);

    /// <summary>Someone else in the chat is typing. Expire the indicator after about 5 seconds.</summary>
    Task Typing(Guid chatId, Guid userId);

    /// <summary>A user who shares a chat with you opened their first connection.</summary>
    Task UserOnline(Guid userId);

    /// <summary>A user who shares a chat with you closed their last connection and did not return within the grace period.</summary>
    Task UserOffline(Guid userId, DateTimeOffset lastSeenAt);
}
