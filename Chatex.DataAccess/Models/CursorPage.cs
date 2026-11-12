namespace Chatex.DataAccess.Models;

/// <summary>One page of a keyset-paginated list. <paramref name="NextCursor"/> is null on the last page.</summary>
public sealed record CursorPage<TItem, TCursor>(IReadOnlyList<TItem> Items, TCursor? NextCursor)
    where TCursor : struct;
