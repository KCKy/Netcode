namespace Core.Utility;

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

    public static int ReadInt(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(int))
            throw new ArgumentException(BufferSmall, nameof(input));

        int value = 0;

        for (int i = 0; i < sizeof(int); i++)
            value |= input[i] << (i * 8);

        return value;
    }
}
