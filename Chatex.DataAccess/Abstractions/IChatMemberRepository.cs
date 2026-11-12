using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;

namespace Chatex.DataAccess.Abstractions;

public interface IChatMemberRepository
{
    void Add(ChatMember member);

    void Remove(ChatMember member);

    Task<ChatMember?> GetAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>The users from <paramref name="userIds"/> who are already members of the chat.</summary>
    Task<IReadOnlyList<Guid>> GetExistingUserIdsAsync(
        Guid chatId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>Members ordered by user id, with user and profile. Pass the previous page's cursor as <paramref name="afterUserId"/>.</summary>
    Task<CursorPage<ChatMember, Guid>> GetMembersAsync(
        Guid chatId, Guid? afterUserId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Ids of the user's (not deleted) chats, e.g. to join SignalR groups on connect.</summary>
    Task<IReadOnlyList<Guid>> GetChatIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the read position to <paramref name="messageId"/> if it is newer than the current one.
    /// Writes immediately; returns false when nothing changed.
    /// </summary>
    Task<bool> MarkReadAsync(Guid chatId, Guid userId, Guid messageId, CancellationToken cancellationToken = default);
}
