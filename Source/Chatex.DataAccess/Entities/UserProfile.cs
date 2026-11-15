namespace Chatex.DataAccess.Entities;

/// <summary>Public profile, one per <see cref="User"/>; created together with the user at registration.</summary>
public class UserProfile : IHasUpdatedAt
{
    public Guid UserId { get; set; }
    public required string DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarPath { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
