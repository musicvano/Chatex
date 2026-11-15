using Chatex.Business.Attachments;
using Chatex.Business.Users;
using Chatex.DataAccess.Entities;

namespace Chatex.Business.Messages;

/// <param name="Body">Null for deleted messages.</param>
public sealed record MessageDto(
    Guid Id,
    Guid ChatId,
    UserSummaryDto Sender,
    string? Body,
    Guid? ReplyToMessageId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    IReadOnlyList<AttachmentDto> Attachments);

/// <param name="Body">May be empty when the message carries attachments.</param>
/// <param name="AttachmentIds">Ids returned by uploads that are not yet attached to a message.</param>
public sealed record SendMessageCommand(string? Body, Guid? ReplyToMessageId = null, IReadOnlyList<Guid>? AttachmentIds = null);

public sealed record EditMessageCommand(string Body);

internal static class MessageMappings
{
    /// <summary>Needs <see cref="Message.Sender"/> with profile and <see cref="Message.Attachments"/> loaded.</summary>
    public static MessageDto ToDto(this Message message)
    {
        var deleted = message.DeletedAt is not null;
        return new MessageDto(
            message.Id,
            message.ChatId,
            message.Sender.ToSummaryDto(),
            deleted ? null : message.Body,
            message.ReplyToMessageId,
            message.CreatedAt,
            message.EditedAt,
            deleted,
            deleted ? [] : message.Attachments.OrderBy(a => a.Id).Select(a => a.ToDto()).ToList());
    }
}
