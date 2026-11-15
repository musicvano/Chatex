using Chatex.Core.Common;

namespace Chatex.Core.Users;

public static class UserErrors
{
    public static Error NotFound { get; } = Error.NotFound("user_not_found", "The user does not exist.");
}
