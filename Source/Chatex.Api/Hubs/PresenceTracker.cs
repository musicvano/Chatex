using System.Collections.Concurrent;

namespace Chatex.Api.Hubs;

/// <summary>
/// The open hub connections of each user, in memory. One process, so no backplane is needed.
/// A user is online while they have at least one connection (tabs and devices count separately).
/// </summary>
internal sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    /// <summary>Adds the connection; returns true when it is the user's first one.</summary>
    public bool Add(Guid userId, string connectionId)
    {
        while (true)
        {
            var set = _connections.GetOrAdd(userId, _ => []);
            lock (set)
            {
                // Remove may have emptied and dropped this set after GetOrAdd returned it; then retry with a fresh one.
                if (_connections.TryGetValue(userId, out var current) && ReferenceEquals(current, set))
                {
                    set.Add(connectionId);
                    return set.Count == 1;
                }
            }
        }
    }

    /// <summary>Removes the connection; returns true when it was the user's last one.</summary>
    public bool Remove(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
        {
            return false;
        }

        lock (set)
        {
            if (!set.Remove(connectionId) || set.Count > 0)
            {
                return false;
            }

            _connections.TryRemove(new KeyValuePair<Guid, HashSet<string>>(userId, set));
            return true;
        }
    }

    public IReadOnlyList<string> GetConnections(Guid userId)
    {
        if (!_connections.TryGetValue(userId, out var set))
        {
            return [];
        }

        lock (set)
        {
            return [.. set];
        }
    }

    public bool IsOnline(Guid userId)
    {
        if (!_connections.TryGetValue(userId, out var set))
        {
            return false;
        }

        lock (set)
        {
            return set.Count > 0;
        }
    }
}
