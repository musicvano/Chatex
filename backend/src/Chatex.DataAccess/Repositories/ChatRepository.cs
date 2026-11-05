using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Chatex.DataAccess.Repositories;

internal sealed class ChatRepository(ChatexDbContext db) : IChatRepository
{
    private const int PreviewLength = 100;

    private IQueryable<Chat> Chats => db.Chats.Where(c => c.DeletedAt == null);

    public void Add(Chat chat) => db.Chats.Add(chat);

    public Task<Chat?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Chats.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Chat?> GetDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        var key = Chat.MakeDirectKey(userA, userB);
        return Chats.FirstOrDefaultAsync(c => c.DirectKey == key, cancellationToken);
    }

    public Task<Chat?> GetByHandleAsync(string handle, CancellationToken cancellationToken = default)
    {
        var normalized = handle.ToLowerInvariant();
        return Chats.FirstOrDefaultAsync(c => c.PublicHandle == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Chat>> SearchPublicAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var pattern = LikePattern.Contains(query);
        return await Chats
            .AsNoTracking()
            .Where(c => c.PublicHandle != null
                && (EF.Functions.ILike(c.PublicHandle, pattern, LikePattern.Escape)
                    || EF.Functions.ILike(c.Title!, pattern, LikePattern.Escape)))
            .OrderBy(c => c.PublicHandle)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPage<ChatListItem, ChatListCursor>> GetUserChatsAsync(
        Guid userId, ChatListCursor? after, int limit, CancellationToken cancellationToken = default)
    {
        var memberships = db.ChatMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Chat.DeletedAt == null);

        if (after is { } cursor)
        {
            memberships = memberships.Where(m => EF.Functions.LessThan(
                ValueTuple.Create(m.Chat.LastActivityAt, m.ChatId),
                ValueTuple.Create(cursor.LastActivityAt, cursor.ChatId)));
        }

        var items = await memberships
            .OrderByDescending(m => m.Chat.LastActivityAt)
            .ThenByDescending(m => m.ChatId)
            .Take(limit + 1)
            .Select(m => new ChatListItem(
                m.Chat,
                m.Role,
                m.IsMuted,
                m.Chat.Messages
                    .Where(x => x.DeletedAt == null)
                    .OrderByDescending(x => x.Id)
                    .Select(x => new MessagePreview(
                        x.Id,
                        x.SenderId,
                        x.Body.Length > PreviewLength ? x.Body.Substring(0, PreviewLength) : x.Body,
                        x.CreatedAt))
                    .FirstOrDefault(),
                m.Chat.Messages.Count(x => x.DeletedAt == null
                    && x.SenderId != userId
                    && (m.LastReadMessageId == null || x.Id.CompareTo(m.LastReadMessageId.Value) > 0)),
                m.Chat.Type == ChatType.Direct
                    ? m.Chat.Members
                        .Where(o => o.UserId != userId)
                        .Select(o => new UserSummary(o.UserId, o.User.UserName!, o.User.Profile!.DisplayName, o.User.Profile.AvatarPath))
                        .FirstOrDefault()
                    : null))
            .ToListAsync(cancellationToken);

        return Paging.ToPage(items, limit, i => new ChatListCursor(i.Chat.LastActivityAt, i.Chat.Id));
    }

    public Task TouchAsync(Guid chatId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        db.Chats
            .Where(c => c.Id == chatId && c.LastActivityAt < at)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastActivityAt, at), cancellationToken);
}
