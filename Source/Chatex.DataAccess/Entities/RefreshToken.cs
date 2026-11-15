namespace Chatex.DataAccess.Entities;

/// <summary>
/// A long-lived token that is exchanged for a new JWT access token. Only a hash of the token is stored.
/// Rotation: on use the token is revoked and <see cref="ReplacedByTokenId"/> points to its successor.
/// </summary>
public class RefreshToken : IHasCreatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedByIp { get; set; }

    public User User { get; set; } = null!;

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
