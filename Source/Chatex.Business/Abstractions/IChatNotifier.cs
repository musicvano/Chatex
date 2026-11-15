using Chatex.Business.Messages;

namespace Chatex.Business.Abstractions;

/// <summary>
/// Pushes chat events to connected clients. Services call it only after the change is committed.
/// The Api implements it with SignalR; until then <see cref="NullChatNotifier"/> drops the events.
/// </summary>
public interface IChatNotifier
{
    Task MessageCreatedAsync(MessageDto message, CancellationToken cancellationToken = default);

    Task MessageUpdatedAsync(MessageDto message, CancellationToken cancellationToken = default);

    Task MessageDeletedAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default);

    Task MessagesReadAsync(Guid chatId, Guid userId, Guid lastReadMessageId, CancellationToken cancellationToken = default);

    /// <summary>Users joined or were added, including everyone in a newly created chat.</summary>
    Task MembersAddedAsync(Guid chatId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>A user left or was removed.</summary>
    Task MemberRemovedAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Title, description, handle, avatar or member roles changed.</summary>
    Task ChatUpdatedAsync(Guid chatId, CancellationToken cancellationToken = default);

    Task ChatDeletedAsync(Guid chatId, CancellationToken cancellationToken = default);
}

public sealed class NullChatNotifier : IChatNotifier
{
    public Task MessageCreatedAsync(MessageDto message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MessageUpdatedAsync(MessageDto message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MessageDeletedAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MessagesReadAsync(Guid chatId, Guid userId, Guid lastReadMessageId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MembersAddedAsync(Guid chatId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MemberRemovedAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ChatUpdatedAsync(Guid chatId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ChatDeletedAsync(Guid chatId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
