namespace Chatex.Business.Common;

/// <summary>What went wrong, coarse enough for the Api to pick an HTTP status from it.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    Unauthorized,
}

/// <summary>An expected failure. <see cref="Code"/> is stable and meant for clients; <see cref="Message"/> is for people.</summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    /// <summary>Per-field messages for validation errors, keyed by property name.</summary>
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }

    public static Error Validation(string code, string message) => new(ErrorKind.Validation, code, message);

    public static Error Validation(string field, string code, string message) =>
        new(ErrorKind.Validation, code, message) { Details = new Dictionary<string, string[]> { [field] = [message] } };

    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);

    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);

    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);
}
