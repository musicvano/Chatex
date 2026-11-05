using Chatex.Business.Common;

namespace Chatex.Business.Messages;

public static class MessageErrors
{
    public static Error NotFound { get; } = Error.NotFound("message_not_found", "The message does not exist.");

    public static Error CannotPost { get; } =
        Error.Forbidden("cannot_post", "Only the owner and admins can post in this channel.");

    public static Error NotSender { get; } = Error.Forbidden("not_sender", "Only the sender can edit a message.");

    public static Error ReplyNotFound { get; } =
        Error.Validation("replyToMessageId", "reply_not_found", "The message you reply to is not in this chat.");

    public static Error InvalidAttachments { get; } =
        Error.Validation("attachmentIds", "invalid_attachments", "Some attachments do not exist, are not yours or are already sent.");
}

internal static class MessageLimits
{
    public const int MaxBody = 4000;
    public const int MaxAttachments = 10;
}
