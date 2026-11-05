using FluentValidation;

namespace Chatex.Business.Users;

internal static class ProfileValidators
{
    public const int MaxDisplayName = 64;
    public const int MaxBio = 500;
}

internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(ProfileValidators.MaxDisplayName);
        RuleFor(c => c.Bio).MaximumLength(ProfileValidators.MaxBio);
    }
}

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().MaximumLength(128);
    }
}
