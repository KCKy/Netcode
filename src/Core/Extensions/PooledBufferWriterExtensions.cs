using Core.Utility;
using MemoryPack;

namespace Core.Extensions;

public static class PooledBufferWriterExtensions
{
    public static Memory<byte> MemoryPackSerialize<T>(ref this PooledBufferWriter<byte> writer, T value)
        where T : class
    {
        MemoryPackSerializer.Serialize(writer, value);
        return writer.ExtractAndReplace();
    }

    public static Memory<byte> MemoryPackSerializeS<T>(ref this PooledBufferWriter<byte> writer, in T value)
        where T : struct
    {
        MemoryPackSerializer.Serialize(writer, value);
        return writer.ExtractAndReplace();
    }
}
