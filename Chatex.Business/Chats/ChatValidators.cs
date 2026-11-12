using Chatex.Business.Common;
using Chatex.DataAccess.Entities;
using FluentValidation;

namespace Chatex.Business.Chats;

internal sealed class CreateChatCommandValidator : AbstractValidator<CreateChatCommand>
{
    public CreateChatCommandValidator()
    {
        RuleFor(c => c.Type)
            .Must(t => t is ChatType.Group or ChatType.Channel)
            .WithMessage("Only groups and channels can be created; open direct chats instead.");
        RuleFor(c => c.Title).NotEmpty().MaximumLength(ChatLimits.MaxTitle);
        RuleFor(c => c.Description).MaximumLength(ChatLimits.MaxDescription);
        RuleFor(c => c.PublicHandle).Handle(5, 32);
        RuleFor(c => c.MemberIds)
            .Must(ids => ids is null || ids.Count < ChatLimits.MaxGroupMembers)
            .WithMessage($"A chat can start with at most {ChatLimits.MaxGroupMembers - 1} other members.");
    }
}

internal sealed class UpdateChatCommandValidator : AbstractValidator<UpdateChatCommand>
{
    public UpdateChatCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(ChatLimits.MaxTitle);
        RuleFor(c => c.Description).MaximumLength(ChatLimits.MaxDescription);
        RuleFor(c => c.PublicHandle).Handle(5, 32);
    }
}
