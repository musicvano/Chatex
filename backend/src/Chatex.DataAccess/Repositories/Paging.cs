using Chatex.DataAccess.Models;

namespace Chatex.DataAccess.Repositories;

internal static class Paging
{
    /// <summary>
    /// Turns <paramref name="items"/>, fetched with <c>Take(limit + 1)</c>, into a page.
    /// The extra row only tells whether another page exists; it is not returned.
    /// </summary>
    public static CursorPage<TItem, TCursor> ToPage<TItem, TCursor>(
        List<TItem> items, int limit, Func<TItem, TCursor> cursorOf)
        where TCursor : struct
    {
        if (items.Count <= limit)
        {
            return new CursorPage<TItem, TCursor>(items, null);
        }

        items.RemoveAt(limit);
        return new CursorPage<TItem, TCursor>(items, cursorOf(items[^1]));
    }
}
