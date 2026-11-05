using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatex.DataAccess.Repositories;

internal sealed class UserRepository(ChatexDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var pattern = LikePattern.Contains(query);
        return await db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .Where(u => EF.Functions.ILike(u.UserName!, pattern, LikePattern.Escape)
                || (u.Profile != null && EF.Functions.ILike(u.Profile.DisplayName, pattern, LikePattern.Escape)))
            .OrderBy(u => u.UserName)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetExistingIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await db.Users.Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken);

    public void AddProfile(UserProfile profile) => db.UserProfiles.Add(profile);

    public Task UpdateLastSeenAsync(Guid userId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default) =>
        db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, lastSeenAt), cancellationToken);
}
