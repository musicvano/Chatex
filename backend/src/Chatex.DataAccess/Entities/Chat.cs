namespace Chatex.DataAccess.Entities;

/// <summary>A conversation: a direct chat, a group or a channel (see <see cref="ChatType"/>).</summary>
public class Chat : IHasCreatedAt, IHasUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public ChatType Type { get; set; }

    /// <summary>Null for direct chats; clients show the other member's name instead.</summary>
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? AvatarPath { get; set; }

    /// <summary>Unique lower-case public @handle that makes a group or channel findable. Null means private.</summary>
    public string? PublicHandle { get; set; }

    /// <summary>Set only for direct chats, see <see cref="MakeDirectKey"/>. Unique, so a pair of users has one direct chat.</summary>
    public string? DirectKey { get; set; }

    public Guid CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Time of the newest message, or creation time. Orders the user's chat list.</summary>
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User CreatedBy { get; set; } = null!;
    public ICollection<ChatMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];

    public static string MakeDirectKey(Guid userA, Guid userB) =>
        userA.CompareTo(userB) <= 0 ? $"{userA:N}:{userB:N}" : $"{userB:N}:{userA:N}";
}
