namespace Chatex.DataAccess.Storage;

public sealed class FileStorageOptions
{
    /// <summary>Directory that holds all uploads, outside the web root (production: <c>/var/lib/chatex/uploads</c>).</summary>
    public string RootPath { get; set; } = "";
}
