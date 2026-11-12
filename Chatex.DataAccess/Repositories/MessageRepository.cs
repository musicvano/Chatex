using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Chatex.DataAccess.Repositories;

internal sealed class MessageRepository(ChatexDbContext db) : IMessageRepository
{
    public void Add(Message message) => db.Messages.Add(message);

    public Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Messages.Include(m => m.Attachments).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Message?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Messages
            .AsNoTracking()
            .Include(m => m.Sender).ThenInclude(u => u.Profile)
            .Include(m => m.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<bool> ExistsInChatAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default) =>
        db.Messages.AnyAsync(m => m.Id == messageId && m.ChatId == chatId && m.DeletedAt == null, cancellationToken);

    public async Task<CursorPage<Message, Guid>> GetPageAsync(
        Guid chatId, Guid? before, Guid? after, int limit, CancellationToken cancellationToken = default)
    {
        if (before is not null && after is not null)
        {
            throw new ArgumentException($"Pass either {nameof(before)} or {nameof(after)}, not both.");
        }

        var query = db.Messages
            .AsNoTracking()
            .Include(m => m.Sender).ThenInclude(u => u.Profile)
            .Include(m => m.Attachments)
            .Where(m => m.ChatId == chatId);

        // UUIDv7 ids sort by creation time, so the id alone is the cursor.
        if (after is { } newerThan)
        {
            query = query.Where(m => m.Id.CompareTo(newerThan) > 0).OrderBy(m => m.Id);
        }
        else
        {
            if (before is { } olderThan)
            {
                query = query.Where(m => m.Id.CompareTo(olderThan) < 0);
            }

            query = query.OrderByDescending(m => m.Id);
        }

        var items = await query.Take(limit + 1).AsSplitQuery().ToListAsync(cancellationToken);
        return Paging.ToPage(items, limit, m => m.Id);
    }
}
