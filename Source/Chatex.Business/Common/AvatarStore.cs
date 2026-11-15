using Chatex.DataAccess.Abstractions;

namespace Chatex.Business.Common;

/// <summary>Limits for avatar images, public so the Api can cap request bodies to match.</summary>
public static class AvatarLimits
{
    public const long MaxBytes = 5 * 1024 * 1024;
}

/// <summary>Validates and stores avatar images for users and chats.</summary>
internal sealed class AvatarStore(IFileStorage storage)
{
    public const long MaxBytes = AvatarLimits.MaxBytes;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    public static Error InvalidType { get; } =
        Error.Validation("contentType", "avatar_invalid_type", "Avatars must be JPEG, PNG or WebP images.");

    public static Error TooLarge { get; } =
        Error.Validation("file", "avatar_too_large", $"Avatars must be at most {MaxBytes / (1024 * 1024)} MB.");

    /// <summary>Stores the image and returns its storage path.</summary>
    public async Task<Result<string>> SaveAsync(FileUpload upload, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(upload.ContentType))
        {
            return InvalidType;
        }

        if (upload.Length > MaxBytes)
        {
            return TooLarge;
        }

        try
        {
            return await storage.SaveAsync(new LimitedReadStream(upload.Content, MaxBytes), cancellationToken);
        }
        catch (FileTooLargeException)
        {
            return TooLarge;
        }
    }

    /// <summary>Deletes a replaced avatar. Called after the database no longer points at it.</summary>
    public Task DeleteAsync(string? storagePath, CancellationToken cancellationToken) =>
        storagePath is null ? Task.CompletedTask : storage.DeleteAsync(storagePath, cancellationToken);
}
