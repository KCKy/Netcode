using MemoryPack;
using System.Drawing;
using Useful;

namespace Core.Utility;

public static class PooledBufferWriterExtensions
{
    public static Memory<byte> MemoryPackSerialize<T>(this PooledBufferWriter<byte> writer, in T value)
    {
        MemoryPackSerializer.Serialize(writer, value);
        return writer.ExtractAndReplace();
    }

    public static void Copy<T>(this PooledBufferWriter<byte> writer, in T original, ref T? copy)
    {
        MemoryPackSerializer.Serialize(writer, original);
        MemoryPackSerializer.Deserialize(writer.WrittenSpan, ref copy);
        writer.Reset();
    }

    public static void Write(this PooledBufferWriter<byte> writer, int value)
    {
        const int size = sizeof(int);
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }

    public static void Write(this PooledBufferWriter<byte> writer, long value)
    {
        const int size = sizeof(long);
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }

    public static void Write(this PooledBufferWriter<byte> writer, long? value)
    {
        const int size = Bits.NullableLongSize;
        var target = writer.GetSpan(size);
        Bits.Write(value, target);
        writer.Advance(size);
    }

    public static void Ignore(this PooledBufferWriter<byte> writer, int amount)
    {
        writer.GetSpan(amount);
        writer.Advance(amount);
    }
}
