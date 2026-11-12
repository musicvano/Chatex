using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatex.DataAccess.Repositories;

internal sealed class AttachmentRepository(ChatexDbContext db) : IAttachmentRepository
{
    public void Add(Attachment attachment) => db.Attachments.Add(attachment);

    public void Remove(Attachment attachment) => db.Attachments.Remove(attachment);

    public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Attachments.Include(a => a.Message).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Attachment>> GetPendingAsync(
        Guid uploaderId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await db.Attachments
            .Where(a => a.UploaderId == uploaderId && a.MessageId == null && ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Attachment>> GetStalePendingAsync(
        DateTimeOffset before, int limit, CancellationToken cancellationToken = default) =>
        await db.Attachments
            .Where(a => a.MessageId == null && a.CreatedAt < before)
            .OrderBy(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
