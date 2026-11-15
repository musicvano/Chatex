using Chatex.Business.Common;

namespace Chatex.Business.Auth;

public static class AuthErrors
{
    /// <summary>Deliberately vague: does not say whether the user exists.</summary>
    public static Error InvalidCredentials { get; } =
        Error.Unauthorized("invalid_credentials", "The user name, email or password is wrong.");

    public static Error LockedOut { get; } =
        Error.Unauthorized("locked_out", "Too many failed attempts. Try again later.");

    public static Error InvalidRefreshToken { get; } =
        Error.Unauthorized("invalid_refresh_token", "The session has expired. Sign in again.");

    public static Error UserNameTaken { get; } =
        Error.Conflict("user_name_taken", "This user name is already taken.");

    public static Error EmailTaken { get; } =
        Error.Conflict("email_taken", "An account with this email already exists.");
}
