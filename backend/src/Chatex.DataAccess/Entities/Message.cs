namespace Chatex.DataAccess.Entities;

/// <summary>
/// A chat message. <see cref="Id"/> is a UUIDv7, so it sorts by creation time and doubles as the pagination cursor.
/// </summary>
public class Message : IHasCreatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ChatId { get; set; }
    public Guid SenderId { get; set; }
    public required string Body { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Soft delete: the row stays so replies and read positions keep pointing at it.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public Chat Chat { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public Message? ReplyTo { get; set; }
    public ICollection<Attachment> Attachments { get; set; } = [];
}
