namespace Chatex.DataAccess.Entities;

/// <summary>Membership of a user in a chat, with role and read position.</summary>
public class ChatMember : IHasCreatedAt
{
    public Guid ChatId { get; set; }
    public Guid UserId { get; set; }
    public ChatMemberRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>Newest message the user has read. Only moves forward; unread = messages with a greater id.</summary>
    public Guid? LastReadMessageId { get; set; }
    public bool IsMuted { get; set; }

    public Chat Chat { get; set; } = null!;
    public User User { get; set; } = null!;

    DateTimeOffset IHasCreatedAt.CreatedAt
    {
        get => JoinedAt;
        set => JoinedAt = value;
    }

    public bool CanPost(ChatType chatType) =>
        chatType != ChatType.Channel || Role is ChatMemberRole.Owner or ChatMemberRole.Admin;
}
