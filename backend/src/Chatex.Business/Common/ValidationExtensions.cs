using System.Text.RegularExpressions;
using FluentValidation;

namespace Chatex.Business.Common;

internal static class ValidationExtensions
{
    /// <summary>Runs the validator; returns null when <paramref name="instance"/> is valid.</summary>
    public static async Task<Error?> ValidateToErrorAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return null;
        }

        return Error.Validation("validation_failed", "One or more fields are invalid.") with
        {
            Details = result.Errors
                .GroupBy(e => CamelCase(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
        };
    }

    /// <summary>Field names as clients see them in JSON: <c>UserName</c> becomes <c>userName</c>.</summary>
    private static string CamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>
    /// A Telegram-style handle: starts with a letter, then letters, digits or underscores. Null passes;
    /// combine with NotEmpty when the handle is required.
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> Handle<T, TProperty>(
        this IRuleBuilder<T, TProperty> rule, int minLength, int maxLength)
    {
        var pattern = new Regex($"^[A-Za-z][A-Za-z0-9_]{{{minLength - 1},{maxLength - 1}}}$", RegexOptions.CultureInvariant);
        return rule
            .Must(value => value is not string text || pattern.IsMatch(text))
            .WithMessage($"Use {minLength}-{maxLength} letters, digits or underscores, starting with a letter.");
    }
}
