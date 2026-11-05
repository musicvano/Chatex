using System.Collections.Concurrent;
using Chatex.Business.Abstractions;
using Chatex.Business.Messages;
using Chatex.DataAccess.Abstractions;

namespace Chatex.Business.Tests;

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public int Count => _files.Count;

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var path = $"test/{Guid.NewGuid():N}";
        _files[path] = copy.ToArray();
        return path;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default) =>
        _files.TryGetValue(storagePath, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(storagePath);

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(storagePath, out _);
        return Task.CompletedTask;
    }
}

/// <summary>Records every event as "Name:chatId".</summary>
public sealed class RecordingNotifier : IChatNotifier
{
    private readonly ConcurrentQueue<string> _events = new();

    public IReadOnlyList<string> Events => [.. _events];

    public Task MessageCreatedAsync(MessageDto message, CancellationToken cancellationToken = default) =>
        Record("MessageCreated", message.ChatId);

    public Task MessageUpdatedAsync(MessageDto message, CancellationToken cancellationToken = default) =>
        Record("MessageUpdated", message.ChatId);

    public Task MessageDeletedAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default) =>
        Record("MessageDeleted", chatId);

    public Task MessagesReadAsync(Guid chatId, Guid userId, Guid lastReadMessageId, CancellationToken cancellationToken = default) =>
        Record("MessagesRead", chatId);

    public Task MembersAddedAsync(Guid chatId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        Record("MembersAdded", chatId);

    public Task MemberRemovedAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default) =>
        Record("MemberRemoved", chatId);

    public Task ChatUpdatedAsync(Guid chatId, CancellationToken cancellationToken = default) => Record("ChatUpdated", chatId);

    public Task ChatDeletedAsync(Guid chatId, CancellationToken cancellationToken = default) => Record("ChatDeleted", chatId);

    private Task Record(string name, Guid chatId)
    {
        _events.Enqueue($"{name}:{chatId}");
        return Task.CompletedTask;
    }
}
