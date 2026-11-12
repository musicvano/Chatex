using Chatex.Business.Chats;
using Chatex.Business.Common;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.Extensions.Options;

namespace Chatex.Business.Attachments;

public interface IAttachmentService
{
    /// <summary>Stores a file for a message the caller is about to send; pass its id in <c>SendMessageCommand.AttachmentIds</c>.</summary>
    Task<Result<AttachmentDto>> UploadAsync(Guid userId, FileUpload upload, CancellationToken cancellationToken = default);

    /// <summary>Allowed for the uploader and for anyone who can see the chat the file was sent to.</summary>
    Task<Result<AttachmentDownload>> GetDownloadAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(AttachmentDownload download, CancellationToken cancellationToken = default);

    /// <summary>Deletes uploads that were never sent within <paramref name="olderThan"/>; returns how many.</summary>
    Task<int> CleanupStaleAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}

internal sealed class AttachmentService(
    IAttachmentRepository attachments,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    ChatAccess chatAccess,
    IOptions<AttachmentOptions> options,
    TimeProvider timeProvider) : IAttachmentService
{
    private const int MaxFileNameLength = 255;
    private const int CleanupBatchSize = 500;

    public async Task<Result<AttachmentDto>> UploadAsync(Guid userId, FileUpload upload, CancellationToken cancellationToken = default)
    {
        var limits = options.Value;
        var contentType = upload.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!limits.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return AttachmentErrors.InvalidType;
        }

        if (upload.Length > limits.MaxBytes)
        {
            return AttachmentErrors.TooLarge;
        }

        var content = new LimitedReadStream(upload.Content, limits.MaxBytes);
        string storagePath;
        try
        {
            storagePath = await storage.SaveAsync(content, cancellationToken);
        }
        catch (FileTooLargeException)
        {
            return AttachmentErrors.TooLarge;
        }

        if (content.Position == 0)
        {
            await storage.DeleteAsync(storagePath, cancellationToken);
            return AttachmentErrors.Empty;
        }

        var attachment = new Attachment
        {
            UploaderId = userId,
            FileName = CleanFileName(upload.FileName),
            ContentType = contentType,
            SizeBytes = content.Position,
            StoragePath = storagePath,
        };
        attachments.Add(attachment);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storagePath, CancellationToken.None);
            throw;
        }

        return attachment.ToDto();
    }

    public async Task<Result<AttachmentDownload>> GetDownloadAsync(
        Guid userId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await attachments.GetByIdAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return AttachmentErrors.NotFound;
        }

        if (attachment.UploaderId != userId)
        {
            if (attachment.Message is null
                || !(await chatAccess.GetVisibleAsync(userId, attachment.Message.ChatId, cancellationToken)).IsSuccess)
            {
                return AttachmentErrors.NotFound;
            }
        }

        return new AttachmentDownload(attachment.StoragePath, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
    }

    public Task<Stream> OpenReadAsync(AttachmentDownload download, CancellationToken cancellationToken = default) =>
        storage.OpenReadAsync(download.StoragePath, cancellationToken);

    public async Task<int> CleanupStaleAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var stale = await attachments.GetStalePendingAsync(timeProvider.GetUtcNow() - olderThan, CleanupBatchSize, cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var attachment in stale)
        {
            attachments.Remove(attachment);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var attachment in stale)
        {
            await storage.DeleteAsync(attachment.StoragePath, cancellationToken);
        }

        return stale.Count;
    }

    /// <summary>Keeps only the last path segment of a client file name, without control characters.</summary>
    private static string CleanFileName(string? fileName)
    {
        var name = fileName ?? "";
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0)
        {
            return "file";
        }

        return name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name;
    }
}
