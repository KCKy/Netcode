using System.Buffers;
using System.Numerics;

namespace Useful;

// Modified version of ArrayPoolBufferWriter{T}.cs from https://github.com/CommunityToolkit/dotnet

public sealed class PooledBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    const int DefaultInitialBufferSize = 256;

    readonly ArrayPool<T> pool_ = ArrayPool<T>.Shared;

    T[] array_;

    int index_ = 0;

    public PooledBufferWriter(int initialCapacity = DefaultInitialBufferSize)
    {
        array_ = pool_.Rent(initialCapacity);
    }

    public ReadOnlyMemory<T> WrittenMemory => array_.AsMemory(0, index_);
    public ReadOnlySpan<T> WrittenSpan => array_.AsSpan(0, index_);

    public Memory<T> Extract()
    {
        var old = array_.AsMemory(0, index_);
        
        array_ = Array.Empty<T>();
        index_ = 0;

        return old;
    }

    public void Reset()
    {
        index_ = 0;
    }

    public Memory<T> ExtractAndReplace()
    {
        var old = array_.AsMemory(0, index_);

        array_ = ArrayPool<T>.Shared.Rent(index_ + 1);
        index_ = 0;

        return old;
    }

    public void Empty()
    {
        var array = array_;
        
        if (array is { Length: > 0 }) 
            pool_.Return(array);

        array_ = Array.Empty<T>();
    }

    public void Advance(int count)
    {
        var array = array_;

        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "The count can't be a negative value.");

        if (index_ > array!.Length - count)
            throw new ArgumentException("The buffer writer has advanced too far.");

        index_ += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return array_.AsMemory(index_);
    }

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
    
    public void Dispose() => Empty();
}
