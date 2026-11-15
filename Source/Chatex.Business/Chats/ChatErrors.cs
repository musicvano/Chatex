using Chatex.Business.Common;

namespace Chatex.Business.Chats;

public static class ChatErrors
{
    /// <summary>Also returned for private chats the caller is not in, so their existence does not leak.</summary>
    public static Error NotFound { get; } = Error.NotFound("chat_not_found", "The chat does not exist.");

    public static Error NotAMember { get; } = Error.Forbidden("not_a_member", "Join the chat first.");

    public static Error NotAllowed { get; } = Error.Forbidden("not_allowed", "You do not have permission to do this.");

    public static Error MemberNotFound { get; } = Error.NotFound("member_not_found", "The user is not a member of this chat.");

    public static Error DirectWithSelf { get; } =
        Error.Validation("userId", "direct_with_self", "You cannot start a direct chat with yourself.");

    public static Error NotForDirectChats { get; } =
        Error.Validation("not_for_direct_chats", "This cannot be done in a direct chat.");

    public static Error HandleTaken { get; } = Error.Conflict("handle_taken", "This public handle is already taken.");

    public static Error OwnerMustTransfer { get; } =
        Error.Conflict("owner_must_transfer", "Make someone else the owner before leaving.");

    public static Error ChatFull { get; } =
        Error.Conflict("chat_full", $"A group can have at most {ChatLimits.MaxGroupMembers} members.");

    public static Error UnknownUsers { get; } =
        Error.Validation("memberIds", "unknown_users", "Some of the users do not exist.");

    public static Error InvalidRole { get; } =
        Error.Validation("role", "invalid_role", "This role is not available in this chat.");

    public static Error UseLeave { get; } =
        Error.Validation("userId", "use_leave", "To remove yourself, leave the chat.");
}

internal static class ChatLimits
{
    public const int MaxGroupMembers = 1000;
    public const int MaxMembersPerRequest = 100;
    public const int MaxTitle = 128;
    public const int MaxDescription = 1000;
}
