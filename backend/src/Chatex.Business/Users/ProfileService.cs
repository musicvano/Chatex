using Chatex.Business.Common;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Chatex.Business.Users;

public interface IProfileService
{
    Task<Result<MyProfileDto>> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<MyProfileDto>> UpdateProfileAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken = default);

    /// <summary>Changes the password and signs the user out of every session.</summary>
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordCommand command, CancellationToken cancellationToken = default);

    Task<Result<MyProfileDto>> SetAvatarAsync(Guid userId, FileUpload upload, CancellationToken cancellationToken = default);

    Task<Result<MyProfileDto>> RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Opens the avatar image of any user.</summary>
    Task<Result<Stream>> OpenAvatarAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<UserSummaryDto[]>> SearchUsersAsync(string query, int? limit, CancellationToken cancellationToken = default);

    /// <summary>Records when the user was last online; the SignalR hub calls it when the last connection closes.</summary>
    Task TouchLastSeenAsync(Guid userId, CancellationToken cancellationToken = default);
}

internal sealed class ProfileService(
    UserManager<User> userManager,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    AvatarStore avatars,
    TimeProvider timeProvider,
    IValidator<UpdateProfileCommand> updateValidator,
    IValidator<ChangePasswordCommand> passwordValidator) : IProfileService
{
    private const int MinQueryLength = 2;

    public async Task<Result<MyProfileDto>> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await users.GetWithProfileAsync(userId, cancellationToken) is { } user ? user.ToMyProfileDto() : UserErrors.NotFound;

    public async Task<Result<UserDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await users.GetWithProfileAsync(userId, cancellationToken) is { } user ? user.ToDto() : UserErrors.NotFound;

    public async Task<Result<MyProfileDto>> UpdateProfileAsync(
        Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken = default)
    {
        if (await updateValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var user = await users.GetWithProfileAsync(userId, cancellationToken);
        if (user?.Profile is null)
        {
            return UserErrors.NotFound;
        }

        user.Profile.DisplayName = command.DisplayName.Trim();
        user.Profile.Bio = string.IsNullOrWhiteSpace(command.Bio) ? null : command.Bio.Trim();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToMyProfileDto();
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId, ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        if (await passwordValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var changed = await userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!changed.Succeeded)
        {
            return changed.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? Error.Validation("currentPassword", "wrong_password", "The current password is wrong.")
                : IdentityErrors.ToValidationError(changed.Errors);
        }

        await refreshTokens.RevokeAllForUserAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return Result.Success;
    }

    public async Task<Result<MyProfileDto>> SetAvatarAsync(
        Guid userId, FileUpload upload, CancellationToken cancellationToken = default)
    {
        var user = await users.GetWithProfileAsync(userId, cancellationToken);
        if (user?.Profile is null)
        {
            return UserErrors.NotFound;
        }

        var saved = await avatars.SaveAsync(upload, cancellationToken);
        if (!saved.IsSuccess)
        {
            return saved.Error;
        }

        var previous = user.Profile.AvatarPath;
        user.Profile.AvatarPath = saved.Value;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await avatars.DeleteAsync(previous, cancellationToken);
        return user.ToMyProfileDto();
    }

    public async Task<Result<MyProfileDto>> RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetWithProfileAsync(userId, cancellationToken);
        if (user?.Profile is null)
        {
            return UserErrors.NotFound;
        }

        var previous = user.Profile.AvatarPath;
        user.Profile.AvatarPath = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await avatars.DeleteAsync(previous, cancellationToken);
        return user.ToMyProfileDto();
    }

    public async Task<Result<Stream>> OpenAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetWithProfileAsync(userId, cancellationToken);
        if (user?.Profile?.AvatarPath is not { } path)
        {
            return CommonErrors.AvatarNotFound;
        }

        return await storage.OpenReadAsync(path, cancellationToken);
    }

    public async Task<Result<UserSummaryDto[]>> SearchUsersAsync(
        string query, int? limit, CancellationToken cancellationToken = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length < MinQueryLength)
        {
            return CommonErrors.QueryTooShort;
        }

        var found = await users.SearchAsync(query, Paging.Clamp(limit), cancellationToken);
        return found.Select(u => u.ToSummaryDto()).ToArray();
    }

    public Task TouchLastSeenAsync(Guid userId, CancellationToken cancellationToken = default) =>
        users.UpdateLastSeenAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
}
