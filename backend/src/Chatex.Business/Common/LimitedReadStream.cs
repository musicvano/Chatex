namespace Chatex.Business.Common;

/// <summary>Thrown by <see cref="LimitedReadStream"/> once more than the allowed number of bytes was read.</summary>
internal sealed class FileTooLargeException() : IOException("The file is larger than allowed.");

/// <summary>
/// Read-only wrapper that fails once the inner stream yields more than <paramref name="maxBytes"/>,
/// so a client cannot get past a size limit by lying about the length.
/// </summary>
internal sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
{
    private long _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await inner.ReadAsync(buffer, cancellationToken));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Count(int read)
    {
        _read += read;
        return _read > maxBytes ? throw new FileTooLargeException() : read;
    }
}
