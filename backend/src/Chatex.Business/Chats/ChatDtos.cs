using Chatex.Business.Users;
using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;

namespace Chatex.Business.Chats;

/// <param name="MyRole">Null when the caller is not a member (viewing a public chat).</param>
/// <param name="Peer">The other user of a direct chat; null for groups and channels.</param>
public sealed record ChatDto(
    Guid Id,
    ChatType Type,
    string? Title,
    string? Description,
    string? PublicHandle,
    bool HasAvatar,
    DateTimeOffset CreatedAt,
    int MemberCount,
    ChatMemberRole? MyRole,
    bool IsMuted,
    UserSummaryDto? Peer);

public sealed record ChatListItemDto(
    Guid Id,
    ChatType Type,
    string? Title,
    bool HasAvatar,
    DateTimeOffset LastActivityAt,
    ChatMemberRole MyRole,
    bool IsMuted,
    MessagePreviewDto? LastMessage,
    int UnreadCount,
    UserSummaryDto? Peer);

/// <summary>A public group or channel found by search.</summary>
public sealed record ChatSummaryDto(
    Guid Id,
    ChatType Type,
    string? Title,
    string? Description,
    string? PublicHandle,
    bool HasAvatar);

public sealed record MessagePreviewDto(Guid Id, Guid SenderId, string Body, DateTimeOffset CreatedAt);

public sealed record ChatMemberDto(UserSummaryDto User, ChatMemberRole Role, DateTimeOffset JoinedAt);

/// <param name="Type">Group or Channel; direct chats are opened with <c>OpenDirectAsync</c>.</param>
/// <param name="PublicHandle">Makes the chat public and findable; null keeps it private.</param>
/// <param name="MemberIds">Users to add besides the creator.</param>
public sealed record CreateChatCommand(
    ChatType Type,
    string Title,
    string? Description = null,
    string? PublicHandle = null,
    IReadOnlyList<Guid>? MemberIds = null);

/// <summary>Replaces the chat's info; a null handle makes the chat private.</summary>
public sealed record UpdateChatCommand(string Title, string? Description, string? PublicHandle);

internal static class ChatMappings
{
    public static ChatListItemDto ToDto(this ChatListItem item) => new(
        item.Chat.Id,
        item.Chat.Type,
        item.Chat.Title,
        item.Chat.AvatarPath is not null,
        item.Chat.LastActivityAt,
        item.Role,
        item.IsMuted,
        item.LastMessage is { } m ? new MessagePreviewDto(m.Id, m.SenderId, m.Body, m.CreatedAt) : null,
        item.UnreadCount,
        item.Peer?.ToDto());

    public static ChatSummaryDto ToSummaryDto(this Chat chat) =>
        new(chat.Id, chat.Type, chat.Title, chat.Description, chat.PublicHandle, chat.AvatarPath is not null);

    public static ChatMemberDto ToDto(this ChatMember member) =>
        new(member.User.ToSummaryDto(), member.Role, member.JoinedAt);
}
