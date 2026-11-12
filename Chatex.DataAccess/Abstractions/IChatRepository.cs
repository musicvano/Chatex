using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;

namespace Chatex.DataAccess.Abstractions;

/// <summary>Direct chats, groups and channels. Soft-deleted chats are never returned.</summary>
public interface IChatRepository
{
    /// <summary>Adds the chat; members added to <see cref="Chat.Members"/> are saved with it.</summary>
    void Add(Chat chat);

    Task<Chat?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Chat?> GetDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);

    /// <summary>Handles are stored lower-case; the lookup lower-cases <paramref name="handle"/>.</summary>
    Task<Chat?> GetByHandleAsync(string handle, CancellationToken cancellationToken = default);

    /// <summary>Public groups and channels whose title or handle contains <paramref name="query"/>. Read-only results.</summary>
    Task<IReadOnlyList<Chat>> SearchPublicAsync(string query, int limit, CancellationToken cancellationToken = default);

    /// <summary>The user's chats, most recently active first, with last message, unread count and direct-chat peer.</summary>
    Task<CursorPage<ChatListItem, ChatListCursor>> GetUserChatsAsync(
        Guid userId, ChatListCursor? after, int limit, CancellationToken cancellationToken = default);

    /// <summary>Moves <see cref="Chat.LastActivityAt"/> forward to <paramref name="at"/>. Writes immediately.</summary>
    Task TouchAsync(Guid chatId, DateTimeOffset at, CancellationToken cancellationToken = default);
}
