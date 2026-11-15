using Microsoft.AspNetCore.Identity;

namespace Chatex.DataAccess.Entities;

/// <summary>
/// Account and credentials (ASP.NET Core Identity). <see cref="IdentityUser{TKey}.UserName"/> is the public @handle.
/// Everything a user shows to others lives in <see cref="Profile"/>.
/// </summary>
public class User : IdentityUser<Guid>, IHasCreatedAt
{
    public User()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }

    public UserProfile? Profile { get; set; }
    public ICollection<ChatMember> Memberships { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
