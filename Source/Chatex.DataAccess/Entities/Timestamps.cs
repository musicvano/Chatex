namespace Chatex.DataAccess.Entities;

/// <summary>Creation time is stamped by <c>TimestampInterceptor</c> when the entity is first saved.</summary>
public interface IHasCreatedAt
{
    DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Update time is stamped by <c>TimestampInterceptor</c> on every insert and update.</summary>
public interface IHasUpdatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}
