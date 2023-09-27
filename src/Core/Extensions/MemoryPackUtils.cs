using Core.Utility;
using MemoryPack;

namespace Core.Extensions;

public static class MemoryPackUtils
{
    public static TObject? DeserializePooled<TObject>(ReadOnlySpan<byte> data) where TObject : class, new()
    {
        TObject original = ObjectPool<TObject>.Create();
        TObject? state = original;
        
        MemoryPackSerializer.Deserialize(data, ref state);

        if (!ReferenceEquals(original, state))
            ObjectPool<TObject>.Destroy(original);

        return state;
    }
}
