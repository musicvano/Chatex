namespace Chatex.DataAccess.Abstractions;

/// <summary>Commits the changes collected by the repositories of the current scope.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> in a database transaction and commits it if the action returns normally.
    /// If it throws, the transaction is rolled back and pending changes are discarded.
    /// Identity's UserManager shares the same DbContext, so its writes join the transaction too.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
