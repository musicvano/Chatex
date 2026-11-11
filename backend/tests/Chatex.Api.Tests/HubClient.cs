using System.Threading.Channels;
using Chatex.Business.Messages;
using Microsoft.AspNetCore.SignalR.Client;

namespace Chatex.Api.Tests;

/// <summary>A hub event as the client received it.</summary>
public sealed record HubEvent(string Name, object?[] Args)
{
    public T Arg<T>(int index) => (T)Args[index]!;
}

/// <summary>
/// A <see cref="HubConnection"/> that records every server event in arrival order, so tests can wait for one
/// and check what came before it. Events on one connection arrive in the order the server sent them.
/// </summary>
public sealed class HubClient : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly Dictionary<string, Type[]> Events = new()
    {
        ["MessageCreated"] = [typeof(MessageDto)],
        ["MessageUpdated"] = [typeof(MessageDto)],
        ["MessageDeleted"] = [typeof(Guid), typeof(Guid)],
        ["MessagesRead"] = [typeof(Guid), typeof(Guid), typeof(Guid)],
        ["MembersAdded"] = [typeof(Guid), typeof(Guid[])],
        ["MemberRemoved"] = [typeof(Guid), typeof(Guid)],
        ["ChatUpdated"] = [typeof(Guid)],
        ["ChatDeleted"] = [typeof(Guid)],
        ["Typing"] = [typeof(Guid), typeof(Guid)],
        ["UserOnline"] = [typeof(Guid)],
        ["UserOffline"] = [typeof(Guid), typeof(DateTimeOffset)],
    };

    private readonly Channel<HubEvent> _received = Channel.CreateUnbounded<HubEvent>();

    public HubClient(HubConnection connection)
    {
        Connection = connection;
        foreach (var (name, types) in Events)
        {
            connection.On(name, types, (args, _) => _received.Writer.WriteAsync(new HubEvent(name, args)).AsTask(), null!);
        }
    }

    public HubConnection Connection { get; }

    /// <summary>Waits for the next <paramref name="name"/> event that matches, skipping everything before it.</summary>
    public async Task<HubEvent> WaitForAsync(string name, Func<HubEvent, bool>? match = null) =>
        (await ReadUntilAsync(name, match))[^1];

    /// <summary>Reads events up to and including the next <paramref name="name"/> event that matches.</summary>
    public async Task<IReadOnlyList<HubEvent>> ReadUntilAsync(string name, Func<HubEvent, bool>? match = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);

        var read = new List<HubEvent>();
        try
        {
            while (true)
            {
                var next = await _received.Reader.ReadAsync(timeout.Token);
                read.Add(next);
                if (next.Name == name && (match is null || match(next)))
                {
                    return read;
                }
            }
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No matching {name} within {Timeout.TotalSeconds}s. Received: {string.Join(", ", read.Select(e => e.Name))}");
        }
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
