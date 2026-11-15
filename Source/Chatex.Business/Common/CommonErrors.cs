namespace Chatex.Business.Common;

public static class CommonErrors
{
    public static Error QueryTooShort { get; } =
        Error.Validation("query", "query_too_short", "Type at least 2 characters to search.");

    public static Error AvatarNotFound { get; } = Error.NotFound("avatar_not_found", "There is no avatar.");
}
