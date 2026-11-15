namespace Chatex.Api.Infrastructure;

/// <summary>Finds an avatar's media type from its first bytes, since only the image itself is stored.</summary>
internal static class ImageSniffer
{
    public const string Fallback = "application/octet-stream";

    private const int HeaderLength = 12;

    /// <summary>Reads the header and returns a stream positioned back at the first byte.</summary>
    public static async Task<(Stream Content, string ContentType)> SniffAsync(Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            var buffered = new MemoryStream();
            await using (content)
            {
                await content.CopyToAsync(buffered, cancellationToken);
            }

            content = buffered;
            content.Position = 0;
        }

        var header = new byte[HeaderLength];
        var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;
        return (content, Detect(header.AsSpan(0, read)));
    }

    public static string Detect(ReadOnlySpan<byte> header) => header switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => Fallback,
    };
}
