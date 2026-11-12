using System.ComponentModel.DataAnnotations;
using Chatex.Business.Common;
using Chatex.DataAccess.Entities;

namespace Chatex.Business.Attachments;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// What the Api needs to serve a file: <see cref="StoragePath"/> for an Nginx X-Accel-Redirect in production,
/// or <c>IAttachmentService.OpenReadAsync</c> to stream it directly.
/// </summary>
public sealed record AttachmentDownload(string StoragePath, string FileName, string ContentType, long SizeBytes);

/// <summary>Upload limits, bound from the <c>Attachments</c> configuration section.</summary>
public sealed class AttachmentOptions
{
    public const string Section = "Attachments";

    [Range(1, 1024L * 1024 * 1024)]
    public long MaxBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Media types that can be uploaded. Anything else is rejected, so no HTML or scripts are served back.</summary>
    [MinLength(1)]
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "application/pdf", "text/plain", "application/zip",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "audio/mpeg", "audio/ogg", "video/mp4",
    ];
}

public static class AttachmentErrors
{
    public static Error NotFound { get; } = Error.NotFound("attachment_not_found", "The file does not exist.");

    public static Error InvalidType { get; } =
        Error.Validation("contentType", "attachment_invalid_type", "This file type cannot be uploaded.");

    public static Error TooLarge { get; } = Error.Validation("file", "attachment_too_large", "The file is too large.");

    public static Error Empty { get; } = Error.Validation("file", "attachment_empty", "The file is empty.");
}

internal static class AttachmentMappings
{
    public static AttachmentDto ToDto(this Attachment attachment) =>
        new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
}
