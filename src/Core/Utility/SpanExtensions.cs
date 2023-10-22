using MemoryPack;

namespace Core.Utility;

public static class SpanExtensions
{
    public static TObject? DeserializePooled<TObject>(this ReadOnlySpan<byte> data)
        where TObject : class, new()
    {
        TObject original = ObjectPool<TObject>.Create();
        TObject? state = original;
        
        MemoryPackSerializer.Deserialize(data, ref state);

        if (!ReferenceEquals(original, state))
            ObjectPool<TObject>.Destroy(original);

        return state;
    }
}
