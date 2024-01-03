using System.Numerics;

namespace Useful;

public struct MinStats<T> where T : struct, IComparisonOperators<T, T, bool>, IMinMaxValue<T>
{
    readonly Queue<T> queue_;
    readonly int length_;

    public MinStats(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "The length of the window must be positive.");

        length_ = length;
        queue_ = new(length);
    }

    public T Add(T value)
    {
        queue_.Enqueue(value);
        if (queue_.Count > length_)
            queue_.Dequeue();

        return queue_.Min();
    }
}
