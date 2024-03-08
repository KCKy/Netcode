using System.Collections.Generic;

namespace Core.DataStructures;

/// <summary>
/// A queue where elements are indexed and may be random accessed.
/// </summary>
/// <typeparam name="T">The type of the contained elements.</typeparam>
public sealed class IndexedQueue<T>
where T : class
{
    readonly Dictionary<long, T> indexToElement_ = new();

    long first_ = 0; // Index of first element which is contained
    long last_ = -1; // Index of last element which is contained

    /// <summary>
    /// Accessor.
    /// </summary>
    /// <param name="index">Index of the element.</param>
    /// <returns>Element at given frame index or null if out of range.</returns>
    public T? this[long index]
    {
        get
        {
            if (index >= first_ && index <= last_)
                return indexToElement_[index];
            return null;
        }
    }

    /// <summary>
    /// Enqueues an element to the queue.
    /// </summary>
    /// <param name="input">Element to add.</param>
    public long Add(T input)
    {
        last_++;

        if (last_ >= first_)
            indexToElement_.Add(last_, input);
        
        return last_;
    }

    /// <summary>
    /// Clears the queue and starts counting elements from <paramref name="frame"/>.
    /// </summary>
    /// <param name="frame">A non-negative value to reset the queue to.</param>
    public void Set(long frame)
    {
        indexToElement_.Clear();
        first_ = frame;
        last_ = frame - 1;
    }

    /// <summary>
    /// Marks all elements lesser than or equal to given index (even those yet to be added) to be deleted.
    /// </summary>
    /// <param name="frame">A non-negative value to reset the queue to.</param>
    public void Pop(long frame)
    {
        while (first_ <= frame)
        {
            indexToElement_.Remove(first_);
            first_++;
        }
    }

    /// <summary>
    /// The next consecutive index for the next <see cref="Add"/>.
    /// </summary>
    public long NextIndex => last_ + 1;

    /// <summary>
    /// The first element in the queue or null of the queue is empty.
    /// </summary>
    public T? First => this[first_];

    /// <summary>
    /// The last element in the queue or null of the queue is empty.
    /// </summary>
    public T? Last => this[last_];
}
