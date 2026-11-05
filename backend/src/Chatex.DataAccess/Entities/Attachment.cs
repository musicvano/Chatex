namespace Chatex.DataAccess.Entities;

/// <summary>
/// A file stored on disk. Uploaded first (with <see cref="MessageId"/> null), then linked to a message when it is sent.
/// </summary>
public class Attachment : IHasCreatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? MessageId { get; set; }
    public Guid UploaderId { get; set; }

    /// <summary>Original client file name, for display only. Never used as a path.</summary>
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>Path relative to the uploads root, e.g. <c>2026/10/{guid}</c>.</summary>
    public required string StoragePath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Message? Message { get; set; }
    public User Uploader { get; set; } = null!;
}
