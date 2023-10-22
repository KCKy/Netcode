namespace Useful;

public static class Bits
{
    const string BufferSmall = "Buffer is too small.";

    public static void Write(int value, Span<byte> output)
    {
        if (output.Length < sizeof(int))
            throw new ArgumentException(BufferSmall, nameof(output));

        for (int i = 0; i < sizeof(int); i++)
            output[i] = (byte)(value >> (i * 8) & 0xFF);
    }

    public static void Write(long value, Span<byte> output)
    {
        if (output.Length < sizeof(long))
            throw new ArgumentException(BufferSmall, nameof(output));

        for (int i = 0; i < sizeof(long); i++)
            output[i] = (byte)(value >> (i * 8) & 0xFF);
    }


    public const int NullableLongSize = sizeof(long) + 1;

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
    
    public static int ReadInt(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(int))
            throw new ArgumentException(BufferSmall, nameof(input));

        int value = 0;

        for (int i = 0; i < sizeof(int); i++)
            value |= input[i] << (i * 8);

        return value;
    }

    public static long ReadLong(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(long))
            throw new ArgumentException(BufferSmall, nameof(input));

        long value = 0;

        for (int i = 0; i < sizeof(long); i++)
            value |= (long)input[i] << (i * 8);

        return value;
    }

    public static long? ReadNullableLong(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(long) + 1)
            throw new ArgumentException(BufferSmall, nameof(input));

        if (input[0] == 0)
            return null;

        return ReadLong(input[1..]);
    }
}
