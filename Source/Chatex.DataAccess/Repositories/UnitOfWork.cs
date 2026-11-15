using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chatex.DataAccess.Repositories;

internal sealed class UnitOfWork(ChatexDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // The failed entities would be saved again by the next call; drop them.
            db.ChangeTracker.Clear();
            throw new UniqueConstraintException(pg.ConstraintName, ex);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                // Disposing the transaction rolls it back; drop the changes it would have saved.
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken);
}
