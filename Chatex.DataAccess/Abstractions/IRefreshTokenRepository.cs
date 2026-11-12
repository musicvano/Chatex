using Chatex.DataAccess.Entities;

namespace Chatex.DataAccess.Abstractions;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);

    /// <summary>Finds a token by its hash, with its user, tracked for revocation.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active token of the user (log out everywhere). Writes immediately; returns the count.</summary>
    Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>Deletes tokens that expired or were revoked before <paramref name="before"/>. Writes immediately.</summary>
    Task<int> DeleteStaleAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}
