namespace Useful;

/// <summary>
/// Utility methods for binary conversions.
/// </summary>
public static class Bits
{
    const string BufferSmall = "Buffer is too small.";

    /// <summary>
    /// Writes given <see cref="int"/> into the start of a span in little endian binary representation.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="output">The span to write to.</param>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static void Write(int value, Span<byte> output)
    {
        if (output.Length < sizeof(int))
            throw new ArgumentException(BufferSmall, nameof(output));

        for (int i = 0; i < sizeof(int); i++)
            output[i] = (byte)(value >> (i * 8) & 0xFF);
    }

    /// <summary>
    /// Writes given <see cref="long"/> into the start of a  span in little endian binary representation.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="output">The span to write to.</param>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static void Write(long value, Span<byte> output)
    {
        if (output.Length < sizeof(long))
            throw new ArgumentException(BufferSmall, nameof(output));

        for (int i = 0; i < sizeof(long); i++)
            output[i] = (byte)(value >> (i * 8) & 0xFF);
    }

    /// <summary>
    /// Size of the binary nullable <see cref="int"/> representation this class uses.
    /// </summary>
    public const int NullableIntSize = sizeof(long) + 1;

    /// <summary>
    /// Writes given nullable <see cref="int"/> into the start of a span in little endian binary representation. 
    /// </summary>
    /// <remarks>
    /// The binary representation of the value itself is preceded by a byte containing 1 if the value is not null. All 5 bytes are zero if null long is provided.
    /// </remarks>
    /// <param name="value">The value to write.</param>
    /// <param name="output">The span to write to.</param>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static void Write(int? value, Span<byte> output)
    {
        if (value is not { } valid)
        {
            output[..NullableIntSize].Clear();
            return;
        }

        output[0] = 1;
        Write(valid, output[1..]);
    }

    /// <summary>
    /// Size of the binary nullable <see cref="int"/> representation this class uses.
    /// </summary>
    public const int NullableLongSize = sizeof(long) + 1;

    /// <summary>
    /// Writes given nullable <see cref="long"/> into the start of a span in little endian binary representation. 
    /// </summary>
    /// <remarks>
    /// The binary representation of the value itself is preceded by a byte containing 1 if the value is not null. All 9 bytes are zero if null long is provided.
    /// </remarks>
    /// <param name="value">The value to write.</param>
    /// <param name="output">The span to write to.</param>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static void Write(long? value, Span<byte> output)
    {
        if (value is not { } valid)
        {
            output[..NullableLongSize].Clear();
            return;
        }

        output[0] = 1;
        Write(valid, output[1..]);
    }
    
    /// <summary>
    /// Reads an <see cref="int"/> in little endian binary representation from given span.
    /// </summary>
    /// <remarks>
    /// Extra bytes in the span are ignored.
    /// </remarks>
    /// <param name="input">Span to read from.</param>
    /// <returns>The read value.</returns>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static int ReadInt(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(int))
            throw new ArgumentException(BufferSmall, nameof(input));

        int value = 0;

        for (int i = 0; i < sizeof(int); i++)
            value |= input[i] << (i * 8);

        return value;
    }

        
    /// <summary>
    /// Reads an <see cref="long"/> in little endian binary representation from given span.
    /// </summary>
    /// <remarks>
    /// Extra bytes in the span are ignored.
    /// </remarks>
    /// <param name="input">Span to read from.</param>
    /// <returns>The read value.</returns>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static long ReadLong(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(long))
            throw new ArgumentException(BufferSmall, nameof(input));

        long value = 0;

        for (int i = 0; i < sizeof(long); i++)
            value |= (long)input[i] << (i * 8);

        return value;
    }

    /// <summary>
    /// Reads a nullable <see cref="int"/> in little endian binary representation from given span.
    /// </summary>
    /// <remarks>
    /// This an inverse function of <see cref="o:Write"/> for nullable <see cref="int"/>.
    /// Extra bytes in the span are ignored.
    /// </remarks>
    /// <param name="input">Span to read from.</param>
    /// <returns>The read value.</returns>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static int? ReadNullableInt(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(int) + 1)
            throw new ArgumentException(BufferSmall, nameof(input));

        if (input[0] == 0)
            return null;

        return ReadInt(input[1..]);
    }

    /// <summary>
    /// Reads a nullable <see cref="long"/> in little endian binary representation from given span.
    /// </summary>
    /// <remarks>
    /// This an inverse function of <see cref="o:Write"/> for nullable <see cref="long"/>.
    /// Extra bytes in the span are ignored.
    /// </remarks>
    /// <param name="input">Span to read from.</param>
    /// <returns>The read value.</returns>
    /// <exception cref="ArgumentException">If given span is not big enough to fit the value.</exception>
    public static long? ReadNullableLong(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(long) + 1)
            throw new ArgumentException(BufferSmall, nameof(input));

        if (input[0] == 0)
            return null;

        return ReadLong(input[1..]);
    }
}
