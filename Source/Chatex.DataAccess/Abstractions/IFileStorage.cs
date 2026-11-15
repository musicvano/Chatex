namespace Chatex.DataAccess.Abstractions;

/// <summary>
/// Stores uploaded files (attachments, avatars). Files are addressed by a storage path the storage picks itself,
/// e.g. <c>2026/11/{guid}</c>; client file names are never part of it.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes <paramref name="content"/> to a new file and returns its storage path.</summary>
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken = default);

    /// <exception cref="FileNotFoundException">No file has this storage path.</exception>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file; does nothing if it does not exist.</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
