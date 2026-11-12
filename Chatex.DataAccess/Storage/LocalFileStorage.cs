using Chatex.DataAccess.Abstractions;
using Microsoft.Extensions.Options;

namespace Chatex.DataAccess.Storage;

/// <summary>Keeps files on the local disk under <see cref="FileStorageOptions.RootPath"/>/yyyy/MM/{guid}.</summary>
internal sealed class LocalFileStorage(IOptions<FileStorageOptions> options, TimeProvider timeProvider) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var storagePath = $"{now:yyyy}/{now:MM}/{Guid.CreateVersion7():N}";
        var fullPath = ToFullPath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(file, cancellationToken);
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }

        return storagePath;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        Stream stream = new FileStream(ToFullPath(storagePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        File.Delete(ToFullPath(storagePath));
        return Task.CompletedTask;
    }

    private string ToFullPath(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storagePath));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Storage path '{storagePath}' points outside the storage root.", nameof(storagePath));
        }

        return fullPath;
    }
}
