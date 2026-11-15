using Microsoft.AspNetCore.Identity;

namespace Chatex.Business.Common;

internal static class IdentityErrors
{
    /// <summary>Turns Identity's errors into one validation error, grouped under the field they concern.</summary>
    public static Error ToValidationError(IEnumerable<IdentityError> errors)
    {
        var details = errors
            .GroupBy(e => FieldOf(e.Code))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        return Error.Validation("validation_failed", "One or more fields are invalid.") with { Details = details };
    }

    private static string FieldOf(string code) => code switch
    {
        _ when code.StartsWith("Password", StringComparison.Ordinal) => "password",
        _ when code.Contains("UserName", StringComparison.Ordinal) => "userName",
        _ when code.Contains("Email", StringComparison.Ordinal) => "email",
        _ => "",
    };
}
