namespace Useful;

public static class SpanExtensions
{
    public static long ReadLong(this ref Span<byte> span)
    {
        long value = Bits.ReadLong(span);
        span = span[sizeof(long)..];
        return value;
    }

    public static long? ReadNullableLong(this ref Span<byte> span)
    {
        long? value = Bits.ReadNullableLong(span);
        span = span[Bits.NullableLongSize..];
        return value;
    }

    public static int ReadInt(this ref Span<byte> span)
    {
        int value = Bits.ReadInt(span);
        span = span[sizeof(int)..];
        return value;
    }
}
