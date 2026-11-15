using Chatex.DataAccess.Entities;

namespace Chatex.DataAccess.Abstractions;

public interface IAttachmentRepository
{
    void Add(Attachment attachment);

    void Remove(Attachment attachment);

    /// <summary>Tracked, with its message (if any) to check access through the message's chat.</summary>
    Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The uploader's attachments from <paramref name="ids"/> not yet linked to a message. Tracked.</summary>
    Task<IReadOnlyList<Attachment>> GetPendingAsync(
        Guid uploaderId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Uploads never linked to a message and created before <paramref name="before"/>. Tracked.</summary>
    Task<IReadOnlyList<Attachment>> GetStalePendingAsync(
        DateTimeOffset before, int limit, CancellationToken cancellationToken = default);
}
