using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Chatex.Business.Chats;
using Chatex.Business.Users;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Chatex.Api.Hubs;

/// <summary>
/// Announces that a user went offline, but only after a grace period, so a page reload or a short network drop
/// does not make them flicker offline and back.
/// </summary>
internal sealed class PresenceService(
    PresenceTracker tracker,
    IHubContext<ChatHub, IChatClient> hub,
    IServiceScopeFactory scopeFactory,
    IOptions<PresenceOptions> options,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<PresenceService> logger)
{
    // The latest pending check per user. A newer disconnect replaces it, so only the last one broadcasts.
    private readonly ConcurrentDictionary<Guid, object> _pending = new();

    /// <summary>Called when the user's last connection closed. Runs in the background.</summary>
    public void ScheduleOffline(Guid userId)
    {
        var ticket = new object();
        _pending[userId] = ticket;
        _ = Task.Run(() => GoOfflineAsync(userId, ticket));
    }

    private async Task GoOfflineAsync(Guid userId, object ticket)
    {
        var cancellationToken = lifetime.ApplicationStopping;
        try
        {
            var grace = TimeSpan.FromSeconds(options.Value.OfflineGraceSeconds);
            if (grace > TimeSpan.Zero)
            {
                await Task.Delay(grace, timeProvider, cancellationToken);
            }

            if (!_pending.TryRemove(new KeyValuePair<Guid, object>(userId, ticket)) || tracker.IsOnline(userId))
            {
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var profiles = scope.ServiceProvider.GetRequiredService<IProfileService>();
            var chats = scope.ServiceProvider.GetRequiredService<IChatService>();

            var lastSeenAt = await profiles.TouchLastSeenAsync(userId, cancellationToken);
            var chatIds = await chats.GetMyChatIdsAsync(userId, cancellationToken);
            if (chatIds.Count > 0)
            {
                await hub.Clients.Groups(chatIds.Select(ChatHub.GroupName).ToList()).UserOffline(userId, lastSeenAt);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down; nobody is left to tell.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to announce that user {UserId} went offline.", userId);
        }
    }
}

internal sealed class PresenceOptions
{
    public const string Section = "Presence";

    /// <summary>How long a user stays online after their last connection closes.</summary>
    [Range(0, 300)]
    public int OfflineGraceSeconds { get; set; } = 5;
}
