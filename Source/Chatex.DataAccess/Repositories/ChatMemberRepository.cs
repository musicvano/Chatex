using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Chatex.DataAccess.Repositories;

internal sealed class ChatMemberRepository(ChatexDbContext db) : IChatMemberRepository
{
    public void Add(ChatMember member) => db.ChatMembers.Add(member);

    public void Remove(ChatMember member) => db.ChatMembers.Remove(member);

    public Task<ChatMember?> GetAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default) =>
        db.ChatMembers.FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId, cancellationToken);

    public Task<bool> IsMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default) =>
        db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == userId, cancellationToken);

    public Task<int> CountAsync(Guid chatId, CancellationToken cancellationToken = default) =>
        db.ChatMembers.CountAsync(m => m.ChatId == chatId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetExistingUserIdsAsync(
        Guid chatId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        await db.ChatMembers
            .Where(m => m.ChatId == chatId && userIds.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

    public async Task<CursorPage<ChatMember, Guid>> GetMembersAsync(
        Guid chatId, Guid? afterUserId, int limit, CancellationToken cancellationToken = default)
    {
        var query = db.ChatMembers
            .AsNoTracking()
            .Include(m => m.User).ThenInclude(u => u.Profile)
            .Where(m => m.ChatId == chatId);

        if (afterUserId is { } cursor)
        {
            query = query.Where(m => m.UserId.CompareTo(cursor) > 0);
        }

        var items = await query.OrderBy(m => m.UserId).Take(limit + 1).ToListAsync(cancellationToken);
        return Paging.ToPage(items, limit, m => m.UserId);
    }

    public async Task<IReadOnlyList<Guid>> GetChatIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.ChatMembers
            .Where(m => m.UserId == userId && m.Chat.DeletedAt == null)
            .Select(m => m.ChatId)
            .ToListAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(Guid chatId, Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var updated = await db.ChatMembers
            .Where(m => m.ChatId == chatId
                && m.UserId == userId
                && (m.LastReadMessageId == null || m.LastReadMessageId.Value.CompareTo(messageId) < 0))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastReadMessageId, messageId), cancellationToken);
        return updated > 0;
    }
}
