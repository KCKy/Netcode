using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Core.Extensions;

/*
public static class MemoryExtensions
{
    public static void ReturnToArrayPool<T>(ref this Memory<T> memory)
    {
        // SOURCE: https://github.com/Cysharp/MemoryPack#deserialize-array-pooling
        var mem = memory;
        memory = Memory<T>.Empty;

        if (!MemoryMarshal.TryGetArray<T>(mem, out var segment) || segment.Array is not { Length: > 0 } array)
            return;

        ArrayPool<T>.Shared.Return(array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }
}
*/
