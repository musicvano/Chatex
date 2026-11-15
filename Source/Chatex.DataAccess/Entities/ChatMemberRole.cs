namespace Chatex.DataAccess.Entities;

public enum ChatMemberRole
{
    Owner,
    Admin,

    /// <summary>Can post. Used in direct chats and groups.</summary>
    Member,

    /// <summary>Read-only member of a channel.</summary>
    Subscriber,
}
