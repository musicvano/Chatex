namespace Chatex.DataAccess.Exceptions;

/// <summary>
/// A save broke a unique index, e.g. a second direct chat for the same pair of users or a taken public handle.
/// Lets callers handle such races without depending on EF Core or Npgsql.
/// </summary>
public sealed class UniqueConstraintException(string? constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
