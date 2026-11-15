using Chatex.DataAccess.Entities;

namespace Chatex.Business.Chats;

/// <summary>Who may do what in a chat. Posting rules live on <see cref="ChatMember.CanPost"/>.</summary>
public static class ChatPermissions
{
    public static bool IsAdmin(ChatMember member) => member.Role is ChatMemberRole.Owner or ChatMemberRole.Admin;

    public static bool CanEditInfo(Chat chat, ChatMember member) => chat.Type != ChatType.Direct && IsAdmin(member);

    public static bool CanManageMembers(Chat chat, ChatMember member) => chat.Type != ChatType.Direct && IsAdmin(member);

    public static bool CanDelete(Chat chat, ChatMember member) =>
        chat.Type != ChatType.Direct && member.Role == ChatMemberRole.Owner;

    /// <summary>Channel subscriber lists are visible to admins only.</summary>
    public static bool CanListMembers(Chat chat, ChatMember member) => chat.Type != ChatType.Channel || IsAdmin(member);

    /// <summary>The owner removes anyone else; an admin removes plain members and subscribers only.</summary>
    public static bool CanRemove(ChatMember actor, ChatMember target) => actor.Role switch
    {
        ChatMemberRole.Owner => target.Role != ChatMemberRole.Owner,
        ChatMemberRole.Admin => target.Role is ChatMemberRole.Member or ChatMemberRole.Subscriber,
        _ => false,
    };

    public static bool CanDeleteMessage(ChatMember member, Message message) =>
        message.SenderId == member.UserId || IsAdmin(member);

    /// <summary>The role of a regular, non-admin member: groups have members, channels have subscribers.</summary>
    public static ChatMemberRole RegularRole(ChatType type) =>
        type == ChatType.Channel ? ChatMemberRole.Subscriber : ChatMemberRole.Member;
}
