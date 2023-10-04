using Core.Utility;
using MemoryPack;

namespace Core.Extensions;

public static class ObjectExtensions
{
    //public static void Destroy<T>(this T source) where T : class, new() => ObjectPool<T>.Destroy(source);

    public static void Copy<T>(this T source, ref T target) where T : class
    {
        byte[] serialized = MemoryPackSerializer.Serialize(source);
        MemoryPackSerializer.Deserialize(serialized, ref target!);
    }

    public static void CopyS<T>(this ref T source, ref T target) where T : struct
    {
        byte[] serializedLevel = MemoryPackSerializer.Serialize(source);
        MemoryPackSerializer.Deserialize(serializedLevel, ref target);
    }
}
