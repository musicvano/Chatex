using FluentValidation;

namespace Chatex.Business.Messages;

internal sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(c => c.Body)
            .NotEmpty()
            .When(c => c.AttachmentIds is not { Count: > 0 })
            .WithMessage("Write a message or attach a file.");
        RuleFor(c => c.Body).MaximumLength(MessageLimits.MaxBody);
        RuleFor(c => c.AttachmentIds)
            .Must(ids => ids is null || ids.Count <= MessageLimits.MaxAttachments)
            .WithMessage($"A message can have at most {MessageLimits.MaxAttachments} attachments.");
    }
}

internal sealed class EditMessageCommandValidator : AbstractValidator<EditMessageCommand>
{
    public EditMessageCommandValidator()
    {
        RuleFor(c => c.Body).NotEmpty().MaximumLength(MessageLimits.MaxBody);
    }
}
