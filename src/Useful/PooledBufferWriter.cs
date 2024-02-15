using System;
using System.Buffers;
using System.Numerics;

namespace Useful;

/// <summary>
/// Implementation of <see cref="IBufferWriter{T}"></see> which uses <see cref="ArrayPool{T}.Shared"></see> to allocate new buffers.
/// Also provides ways to take ownership to extract a written buffer.
/// </summary>
/// <remarks>
/// This a modified version of ArrayPoolBufferWriter{T}.cs from https://github.com/CommunityToolkit/dotnet.
/// </remarks>
/// <typeparam name="T">The type if the written element.</typeparam>
public sealed class PooledBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    const int DefaultInitialBufferSize = 256;

    readonly ArrayPool<T> pool_ = ArrayPool<T>.Shared;

    T[] array_;

    int index_ = 0;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the writing buffer.</param>
    public PooledBufferWriter(int initialCapacity = DefaultInitialBufferSize)
    {
        array_ = pool_.Rent(initialCapacity);
    }

    /// <summary>
    /// Access the written data.
    /// </summary>
    public ReadOnlyMemory<T> WrittenMemory => array_.AsMemory(0, index_);
    
    /// <summary>
    /// Access the written data.
    /// </summary>
    public ReadOnlySpan<T> WrittenSpan => array_.AsSpan(0, index_);

    /// <summary>
    /// Take ownership of the written data. The writer is going to write to a new buffer as if just created.
    /// </summary>
    /// <returns>
    /// The memory spanning the written data.
    /// </returns>
    /// <remarks>
    /// The memory is backed by an array from the array pool. It is the caller's responsibility to return it to the pool.
    /// </remarks>
    public Memory<T> Extract()
    {
        var old = array_.AsMemory(0, index_);
        
        array_ = Array.Empty<T>();
        index_ = 0;

        return old;
    }

    /// <summary>
    /// Throw away the currently written data.
    /// </summary>
    public void Reset()
    {
        index_ = 0;
    }

    /// <summary>
    /// Does the same as <see cref="Extract"></see> but additionally reserves a new buffer of the same size.
    /// </summary>
    /// <returns>
    /// The memory spanning the written data.
    /// </returns>
    /// <remarks>
    /// The memory is backed by an array from the array pool. It is the caller's responsibility to return it to the pool.
    /// </remarks>
    public Memory<T> ExtractAndReplace()
    {
        var old = array_.AsMemory(0, index_);

        array_ = ArrayPool<T>.Shared.Rent(index_ + 1);
        index_ = 0;

        return old;
    }

    /// <summary>
    /// Throw away the currently written data and return the backing buffer to the pool.
    /// </summary>
    public void Empty()
    {
        var array = array_;
        
        if (array is { Length: > 0 }) 
            pool_.Return(array);

        array_ = Array.Empty<T>();
    }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        var array = array_;

        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "The count can't be a negative value.");

        if (index_ > array!.Length - count)
            throw new ArgumentException("The buffer writer has advanced too far.");

        index_ += count;
    }

    /// <inheritdoc/>
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return array_.AsMemory(index_);
    }

    /// <inheritdoc/>
    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return array_.AsSpan(index_);
    }
    
    void EnsureCapacity(int sizeHint)
    {
        var array = array_;

        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint), "The size hint can't be a negative value.");

        if (sizeHint == 0)
            sizeHint = 1;

        if (sizeHint > array!.Length - index_)
            ResizeBuffer(sizeHint);
    }

    void ResizeBuffer(int sizeHint)
    {
        int minimumSize = index_ + sizeHint;

        // The ArrayPool<T> class has a maximum threshold of 1024 * 1024 for the maximum length of
        // pooled arrays, and once this is exceeded it will just allocate a new array every time
        // of exactly the requested size. In that case, we manually round up the requested size to
        // the nearest power of two, to ensure that repeated consecutive writes when the array in
        // use is bigger than that threshold don't end up causing a resize every single time.
        if (minimumSize > 1024 * 1024)
            minimumSize = (int)BitOperations.RoundUpToPowerOf2((uint)minimumSize);

        Resize(pool_, ref array_!, minimumSize);
    }

    static void Resize(ArrayPool<T> pool, ref T[]? array, int newSize, bool clearArray = false)
    {
        // If the old array is null, just create a new one with the requested size
        if (array is null)
        {
            array = pool.Rent(newSize);

            return;
        }

        // If the new size is the same as the current size, do nothing
        if (array.Length == newSize)
        {
            return;
        }

        // Rent a new array with the specified size, and copy as many items from the current array
        // as possible to the new array. This mirrors the behavior of the Array.Resize API from
        // the BCL: if the new size is greater than the length of the current array, copy all the
        // items from the original array into the new one. Otherwise, copy as many items as possible,
        // until the new array is completely filled, and ignore the remaining items in the first array.
        T[] newArray = pool.Rent(newSize);
        int itemsToCopy = Math.Min(array.Length, newSize);

        Array.Copy(array, 0, newArray, 0, itemsToCopy);

        pool.Return(array, clearArray);

        array = newArray;
    }
    
    /// <summary>
    /// Returns the backing buffer to the pool.
    /// </summary>
    public void Dispose() => Empty();
}
