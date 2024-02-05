using System.Buffers;
using System.Runtime.InteropServices;

namespace Useful;

/// <summary>
/// Utility extension methods for <see cref="ArrayPool{T}"/>.
/// </summary>
public static class ArrayPoolExtensions
{
    /// <summary>
    /// Rent an array and turn into a memory.
    /// </summary>
    /// <typeparam name="T">Type of the array.</typeparam>
    /// <param name="pool">The pool to rent from.</param>
    /// <param name="size">The required size of the array.</param>
    /// <returns>Memory exactly <paramref name="size"/> long.</returns>
    /// <remarks>The backing array may be larger than the memory.</remarks>
    public static Memory<T> RentMemory<T>(this ArrayPool<T> pool, int size) => pool.Rent(size).AsMemory(0, size);

    /// <summary>
    /// Return the backing array of a memory to an array pool.
    /// </summary>
    /// <remarks>
    /// The array is cleared if it contains references in some way.
    /// </remarks>
    /// <param name="pool">The pool to return to.</param>
    /// <param name="memory">The memory which is backed by a pooled array.</param>
    public static void Return(this ArrayPool<byte> pool, Memory<byte> memory)
    {
        // SOURCE: https://github.com/Cysharp/MemoryPack#deserialize-array-pooling
        if (!MemoryMarshal.TryGetArray<byte>(memory, out var segment) || segment.Array is not { Length: > 0 } array)
            return;

        pool.Return(array);
    }
}
