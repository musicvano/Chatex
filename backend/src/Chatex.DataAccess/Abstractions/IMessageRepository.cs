using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;

namespace Chatex.DataAccess.Abstractions;

public interface IMessageRepository
{
    void Add(Message message);

    /// <summary>Tracked, with attachments, for editing or deleting.</summary>
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only, with sender profile and attachments, as history pages return it.</summary>
    Task<Message?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsInChatAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keyset page of a chat's history, with sender profile and attachments. Read-only results.
    /// <list type="bullet">
    /// <item>Neither cursor: newest messages, newest first.</item>
    /// <item><paramref name="before"/>: older messages, newest first (scrolling up).</item>
    /// <item><paramref name="after"/>: newer messages, oldest first (filling the gap after a reconnect).</item>
    /// </list>
    /// <see cref="CursorPage{TItem,TCursor}.NextCursor"/> is the id to pass in the same direction for the next page.
    /// </summary>
    Task<CursorPage<Message, Guid>> GetPageAsync(
        Guid chatId, Guid? before, Guid? after, int limit, CancellationToken cancellationToken = default);
}
