using Chatex.DataAccess.Entities;

namespace Chatex.DataAccess.Models;

/// <summary>A row of a user's chat list.</summary>
/// <param name="Peer">The other member of a direct chat; null for groups and channels.</param>
public sealed record ChatListItem(
    Chat Chat,
    ChatMemberRole Role,
    bool IsMuted,
    MessagePreview? LastMessage,
    int UnreadCount,
    UserSummary? Peer);

public sealed record MessagePreview(Guid Id, Guid SenderId, string Body, DateTimeOffset CreatedAt);

public sealed record UserSummary(Guid Id, string UserName, string DisplayName, string? AvatarPath);

/// <summary>Position in the chat list, which is ordered by (LastActivityAt, ChatId) descending.</summary>
public readonly record struct ChatListCursor(DateTimeOffset LastActivityAt, Guid ChatId);
