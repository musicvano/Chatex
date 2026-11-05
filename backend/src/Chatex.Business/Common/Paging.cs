using System.Buffers.Binary;
using System.Buffers.Text;
using Chatex.DataAccess.Models;

namespace Chatex.Business.Common;

/// <summary>One page of a list. Pass <see cref="NextCursor"/> back to get the next page; it is null on the last one.</summary>
public sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);

internal static class Paging
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static Error InvalidCursor { get; } = Error.Validation("cursor", "invalid_cursor", "The cursor is not valid.");

    public static int Clamp(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
}

/// <summary>
/// Opaque cursors: clients pass them back unchanged and must not parse them, so their format can change.
/// </summary>
internal static class CursorCodec
{
    private const int GuidLength = 16;
    private const int ChatListLength = sizeof(long) + GuidLength;

    public static string? Encode(Guid? id) =>
        id is { } value ? Base64Url.EncodeToString(value.ToByteArray(bigEndian: true)) : null;

    public static string? Encode(ChatListCursor? cursor)
    {
        if (cursor is not { } value)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[ChatListLength];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value.LastActivityAt.UtcTicks);
        value.ChatId.TryWriteBytes(bytes[sizeof(long)..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>Decodes a Guid cursor; a null or empty cursor means "first page".</summary>
    public static bool TryDecode(string? cursor, out Guid? id)
    {
        id = null;
        if (string.IsNullOrEmpty(cursor))
        {
            return true;
        }

        Span<byte> bytes = stackalloc byte[GuidLength];
        if (!Base64Url.TryDecodeFromChars(cursor, bytes, out var written) || written != GuidLength)
        {
            return false;
        }

        id = new Guid(bytes, bigEndian: true);
        return true;
    }

    public static bool TryDecode(string? cursor, out ChatListCursor? chatListCursor)
    {
        chatListCursor = null;
        if (string.IsNullOrEmpty(cursor))
        {
            return true;
        }

        Span<byte> bytes = stackalloc byte[ChatListLength];
        if (!Base64Url.TryDecodeFromChars(cursor, bytes, out var written) || written != ChatListLength)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        chatListCursor = new ChatListCursor(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[sizeof(long)..], bigEndian: true));
        return true;
    }
}
