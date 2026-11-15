using Chatex.DataAccess.Entities;
using Chatex.DataAccess.Models;

namespace Chatex.Business.Users;

/// <summary>The signed-in user's own profile.</summary>
public sealed record MyProfileDto(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string? Bio,
    bool HasAvatar,
    DateTimeOffset CreatedAt);

/// <summary>Another user's public profile.</summary>
public sealed record UserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Bio,
    bool HasAvatar,
    DateTimeOffset? LastSeenAt);

/// <summary>Just enough to show a user next to a message or in a list.</summary>
public sealed record UserSummaryDto(Guid Id, string UserName, string DisplayName, bool HasAvatar);

public sealed record UpdateProfileCommand(string DisplayName, string? Bio);

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

internal static class UserMappings
{
    public static MyProfileDto ToMyProfileDto(this User user) => new(
        user.Id,
        user.UserName!,
        user.Email!,
        user.Profile?.DisplayName ?? user.UserName!,
        user.Profile?.Bio,
        user.Profile?.AvatarPath is not null,
        user.CreatedAt);

    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.UserName!,
        user.Profile?.DisplayName ?? user.UserName!,
        user.Profile?.Bio,
        user.Profile?.AvatarPath is not null,
        user.LastSeenAt);

    public static UserSummaryDto ToSummaryDto(this User user) => new(
        user.Id,
        user.UserName!,
        user.Profile?.DisplayName ?? user.UserName!,
        user.Profile?.AvatarPath is not null);

    public static UserSummaryDto ToDto(this UserSummary user) =>
        new(user.Id, user.UserName, user.DisplayName, user.AvatarPath is not null);
}
