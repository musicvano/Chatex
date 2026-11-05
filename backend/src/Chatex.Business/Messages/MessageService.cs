using Chatex.Business.Abstractions;
using Chatex.Business.Chats;
using Chatex.Business.Common;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using FluentValidation;

namespace Chatex.Business.Messages;

public interface IMessageService
{
    Task<Result<MessageDto>> SendAsync(Guid userId, Guid chatId, SendMessageCommand command, CancellationToken cancellationToken = default);

    /// <summary>Only the sender can edit, and only messages that are not deleted.</summary>
    Task<Result<MessageDto>> EditAsync(Guid userId, Guid messageId, EditMessageCommand command, CancellationToken cancellationToken = default);

    /// <summary>The sender or a chat admin can delete. The text and attachments are removed; a placeholder stays.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of history. No cursor: newest messages, newest first. <paramref name="before"/>: older messages, newest first.
    /// <paramref name="after"/>: newer messages, oldest first (catching up after a reconnect).
    /// </summary>
    Task<Result<PageDto<MessageDto>>> GetHistoryAsync(
        Guid userId, Guid chatId, string? before, string? after, int? limit, CancellationToken cancellationToken = default);
}

internal sealed class MessageService(
    IMessageRepository messages,
    IChatRepository chats,
    IAttachmentRepository attachments,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    ChatAccess chatAccess,
    IChatNotifier notifier,
    TimeProvider timeProvider,
    IValidator<SendMessageCommand> sendValidator,
    IValidator<EditMessageCommand> editValidator) : IMessageService
{
    public async Task<Result<MessageDto>> SendAsync(
        Guid userId, Guid chatId, SendMessageCommand command, CancellationToken cancellationToken = default)
    {
        if (await sendValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var access = await chatAccess.GetMembershipAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var (chat, member) = access.Value;
        if (!member.CanPost(chat.Type))
        {
            return MessageErrors.CannotPost;
        }

        if (command.ReplyToMessageId is { } replyTo && !await messages.ExistsInChatAsync(chatId, replyTo, cancellationToken))
        {
            return MessageErrors.ReplyNotFound;
        }

        var attachmentIds = (command.AttachmentIds ?? []).Distinct().ToList();
        var pending = attachmentIds.Count == 0
            ? []
            : await attachments.GetPendingAsync(userId, attachmentIds, cancellationToken);
        if (pending.Count != attachmentIds.Count)
        {
            return MessageErrors.InvalidAttachments;
        }

        var message = new Message
        {
            ChatId = chatId,
            SenderId = userId,
            Body = command.Body?.Trim() ?? "",
            ReplyToMessageId = command.ReplyToMessageId,
        };
        messages.Add(message);
        foreach (var attachment in pending)
        {
            attachment.MessageId = message.Id;
        }

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await unitOfWork.SaveChangesAsync(ct);
            await chats.TouchAsync(chatId, message.CreatedAt, ct);
        }, cancellationToken);

        var dto = await LoadDtoAsync(message.Id, cancellationToken);
        await notifier.MessageCreatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<Result<MessageDto>> EditAsync(
        Guid userId, Guid messageId, EditMessageCommand command, CancellationToken cancellationToken = default)
    {
        if (await editValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var message = await messages.GetByIdAsync(messageId, cancellationToken);
        if (message is null || message.DeletedAt is not null)
        {
            return MessageErrors.NotFound;
        }

        var access = await chatAccess.GetMembershipAsync(userId, message.ChatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        if (message.SenderId != userId)
        {
            return MessageErrors.NotSender;
        }

        message.Body = command.Body.Trim();
        message.EditedAt = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = await LoadDtoAsync(messageId, cancellationToken);
        await notifier.MessageUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await messages.GetByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            return MessageErrors.NotFound;
        }

        var access = await chatAccess.GetMembershipAsync(userId, message.ChatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        if (message.DeletedAt is not null)
        {
            return Result.Success;
        }

        if (!ChatPermissions.CanDeleteMessage(access.Value.Member, message))
        {
            return ChatErrors.NotAllowed;
        }

        var files = message.Attachments.Select(a => a.StoragePath).ToList();
        foreach (var attachment in message.Attachments.ToList())
        {
            attachments.Remove(attachment);
        }

        message.DeletedAt = timeProvider.GetUtcNow();
        message.Body = "";
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var file in files)
        {
            await storage.DeleteAsync(file, cancellationToken);
        }

        await notifier.MessageDeletedAsync(message.ChatId, messageId, cancellationToken);
        return Result.Success;
    }

    public async Task<Result<PageDto<MessageDto>>> GetHistoryAsync(
        Guid userId, Guid chatId, string? before, string? after, int? limit, CancellationToken cancellationToken = default)
    {
        if (!CursorCodec.TryDecode(before, out Guid? beforeId) || !CursorCodec.TryDecode(after, out Guid? afterId))
        {
            return Paging.InvalidCursor;
        }

        if (beforeId is not null && afterId is not null)
        {
            return Error.Validation("cursor", "both_cursors", "Pass either 'before' or 'after', not both.");
        }

        var access = await chatAccess.GetVisibleAsync(userId, chatId, cancellationToken);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        var page = await messages.GetPageAsync(chatId, beforeId, afterId, Paging.Clamp(limit), cancellationToken);
        return new PageDto<MessageDto>(page.Items.Select(m => m.ToDto()).ToList(), CursorCodec.Encode(page.NextCursor));
    }

    private async Task<MessageDto> LoadDtoAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await messages.GetDetailsAsync(messageId, cancellationToken)
            ?? throw new InvalidOperationException($"Message {messageId} was just saved but could not be loaded.");
        return message.ToDto();
    }
}
