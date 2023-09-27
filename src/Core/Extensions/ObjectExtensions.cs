using Core.Utility;
using MemoryPack;

namespace Core.Extensions;

public static class ObjectExtensions
{
    public static void Destroy<T>(this T obj) where T : class, new() => ObjectPool<T>.Destroy(obj);

    public static TObject? Replace<TObject>(this TObject? state, ReadOnlySpan<byte> serialized)
        where TObject : class, new()
    {
        TObject? original = state;

        MemoryPackSerializer.Deserialize(serialized, ref state);

        if (original is not null && !ReferenceEquals(original, state))
            ObjectPool<TObject>.Destroy(original);

        return state;
    }
}
