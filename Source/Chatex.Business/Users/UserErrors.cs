using Chatex.Business.Common;

namespace Chatex.Business.Users;

public static class UserErrors
{
    public static Error NotFound { get; } = Error.NotFound("user_not_found", "The user does not exist.");
}
