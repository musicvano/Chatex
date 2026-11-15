using Chatex.Core.Common;
using Chatex.Core.Users;
using FluentValidation;

namespace Chatex.Core.Auth;

internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.UserName).NotEmpty().Handle(3, 32);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(ProfileValidators.MaxDisplayName);
    }
}

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Login).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
    }
}
