using CommunityToolkit.HighPerformance.Buffers;
using Core.Utility;
using MemoryPack;

namespace Core.Extensions;

public static class ObjectExtensions
{
    public static void Destroy<T>(this T source) where T : class, new() => ObjectPool<T>.Destroy(source);

    public static void Copy<T>(this T source, ref T target)
        where T : class
    {
        using PooledBufferWriter<byte> writer = new();

        MemoryPackSerializer.Serialize(writer, source);
        MemoryPackSerializer.Deserialize(writer.WrittenSpan, ref target!);
    }

    public static void CopyS<T>(this ref T source, ref T target)
        where T : struct
    {
        using PooledBufferWriter<byte> writer = new();

        MemoryPackSerializer.Serialize(writer, source);
        MemoryPackSerializer.Deserialize(writer.WrittenSpan, ref target);
    }
}
