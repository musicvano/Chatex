using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chatex.DataAccess;

/// <summary>
/// Runs <c>SELECT 1</c> on a plain connection. It deliberately bypasses the EF execution strategy: retrying a dead
/// database for half a minute would only delay the 503 that tells the deploy to roll back.
/// </summary>
internal sealed class DatabaseHealthCheck(ChatexDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
            }
            finally
            {
                await connection.CloseAsync();
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("The database cannot be reached.", ex);
        }
    }
}
