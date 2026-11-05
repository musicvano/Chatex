using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatex.DataAccess.Repositories;

internal sealed class RefreshTokenRepository(ChatexDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, revokedAt), cancellationToken);

    public Task<int> DeleteStaleAsync(DateTimeOffset before, CancellationToken cancellationToken = default) =>
        db.RefreshTokens
            .Where(t => t.ExpiresAt < before || t.RevokedAt < before)
            .ExecuteDeleteAsync(cancellationToken);
}
