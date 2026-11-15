using System.ComponentModel.DataAnnotations;
using Chatex.Core.Attachments;
using Chatex.Core.Auth;
using Microsoft.Extensions.Options;

namespace Chatex.Api.Maintenance;

internal sealed class MaintenanceOptions
{
    public const string Section = "Maintenance";

    public bool Enabled { get; set; } = true;

    /// <summary>How often the cleanup runs; the first run is right after startup.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Uploads that were never sent with a message are deleted after this long.</summary>
    [Range(typeof(TimeSpan), "00:10:00", "30.00:00:00")]
    public TimeSpan StaleUploadAge { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Deletes uploads that were never sent and refresh tokens that can no longer be used. Each pass runs in its own DI
/// scope, like a request; a failed pass is logged and the next one runs on schedule.
/// </summary>
internal sealed partial class MaintenanceService(
    IServiceScopeFactory scopes,
    IOptions<MaintenanceOptions> options,
    TimeProvider timeProvider,
    ILogger<MaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.Interval, timeProvider);
        do
        {
            await RunOnceAsync(settings, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(MaintenanceOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

            var uploads = 0;
            int batch;
            do
            {
                batch = await attachments.CleanupStaleAsync(settings.StaleUploadAge, cancellationToken);
                uploads += batch;
            }
            while (batch > 0);

            var tokens = await auth.DeleteStaleRefreshTokensAsync(cancellationToken);
            if (uploads > 0 || tokens > 0)
            {
                LogCleaned(uploads, tokens);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Maintenance deleted {Uploads} stale uploads and {Tokens} stale refresh tokens")]
    private partial void LogCleaned(int uploads, int tokens);

    [LoggerMessage(Level = LogLevel.Error, Message = "Maintenance pass failed; the next one runs on schedule")]
    private partial void LogFailed(Exception exception);
}
