using Chatex.DataAccess.Entities;

namespace Chatex.DataAccess.Abstractions;

/// <summary>
/// Users and their profiles. Creating users, passwords and email confirmation go through Identity's
/// <c>UserManager&lt;User&gt;</c>, which uses the same DbContext.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive substring search on user name and display name. Read-only results.</summary>
    Task<IReadOnlyList<User>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);

    /// <summary>The ids from <paramref name="ids"/> that belong to existing users.</summary>
    Task<IReadOnlyList<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    void AddProfile(UserProfile profile);

    /// <summary>Writes immediately, without <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    Task UpdateLastSeenAsync(Guid userId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default);
}
