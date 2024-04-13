using System;

namespace Kcky.Useful;

/// <summary>
/// Extension methods for <see cref="T:Span{byte}"/>.
/// </summary>
public static class SpanExtensions
{
    /// <summary>
    /// Read <see cref="long"></see> from the start of the span in little endian and advance the start of the span by the size of <see cref="long"/>.
    /// </summary>
    /// <param name="span">The span to read from.</param>
    /// <returns>The read value.</returns>
    public static long ReadLong(this ref Span<byte> span)
    {
        long value = Bits.ReadLong(span);
        span = span[sizeof(long)..];
        return value;
    }

    /// <summary>
    /// Read nullable <see cref="long"></see> from the start of the span in little endian and advance the start of the span by the size of <see cref="long"/> + 1 (9 bytes).
    /// </summary>
    /// <remarks>
    /// Refer to <see cref="Bits.ReadNullableLong"></see> for details about the binary representation.
    /// </remarks>
    /// <param name="span">The span to read from.</param>
    /// <returns>The read value.</returns>
    public static long? ReadNullableLong(this ref Span<byte> span)
    {
        long? value = Bits.ReadNullableLong(span);
        span = span[Bits.NullableLongSize..];
        return value;
    }

    /// <summary>
    /// Read <see cref="int"></see> from the start of the span in little endian and advance the start of the span by the size of <see cref="int"/>.
    /// </summary>
    /// <param name="span">The span to read from.</param>
    /// <returns>The read value.</returns>
    public static int ReadInt(this ref Span<byte> span)
    {
        int value = Bits.ReadInt(span);
        span = span[sizeof(int)..];
        return value;
    }
}
