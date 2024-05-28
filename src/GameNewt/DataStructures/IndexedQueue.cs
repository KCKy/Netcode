using System.Collections.Generic;
using System;

namespace Kcky.GameNewt.DataStructures;

/// <summary>
/// A queue where elements are indexed and may be random accessed.
/// </summary>
/// <typeparam name="T">The type of the contained elements.</typeparam>
sealed class IndexedQueue<T>
{
    readonly Dictionary<long, T> indexToElement_ = new();

    long first_ = 0; // Index of first element which is contained
    long last_ = -1; // Index of last element which is contained

    bool IsContained(long index) => index >= first_ && index <= last_;

    /// <summary>
    /// Accessor.
    /// </summary>
    /// <param name="index">Index of the element.</param>
    /// <exception cref="IndexOutOfRangeException">If the given index is out of range.</exception>
    public T this[long index]
    {
        get
        {
            if (IsContained(index))
                return indexToElement_[index];

            throw new IndexOutOfRangeException("Given index is out of the queue range.");
        }
    }

    /// <summary>
    /// Try to access element.
    /// </summary>
    /// <param name="index">The index to access.</param>
    /// <param name="element"></param>
    /// <returns>Whether the given element was contained in the queue.</returns>
    public bool TryGet(long index, out T element)
    {
        if (IsContained(index))
        {
            element = indexToElement_[index];
            return true;
        }

        element = default!;
        return false;
    }

    /// <summary>
    /// Enqueues an element to the queue.
    /// </summary>
    /// <param name="value">Element to add.</param>
    public long Add(T value)
    {
        last_++;

        if (last_ >= first_)
            indexToElement_.Add(last_, value);
        
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
    /// Whether the queue is empty.
    /// </summary>
    public bool Empty => first_ > last_;

    /// <summary>
    /// The index of the first contained element.
    /// </summary>
    public long FirstIndex => first_;

    /// <summary>
    /// The index of the last contained element.
    /// </summary>
    public long LastIndex => last_;

    /// <summary>
    /// The first element in the queue or null of the queue is empty.
    /// </summary>
    public T? First => this[first_];

    /// <summary>
    /// The last element in the queue or null of the queue is empty.
    /// </summary>
    public T? Last => this[last_];
}
