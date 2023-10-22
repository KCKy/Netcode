using System.Numerics;

namespace Useful;

public struct MovingAverage<T> where T : struct, IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
{
    readonly T[] values_;
    int current_ = 0;

    public MovingAverage(int length, T initial = default)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "The length of the window must be positive.");

        values_ = new T[length];

        for (int i = 0; i < length; i++)
            values_[i] = initial;

        Update();
    }

    (T sum, int length) Update()
    {
        T sum = T.AdditiveIdentity;

        int length = values_.Length;
        for (int i = 0; i < length; i++)
        {
            sum += values_[i];
        }

        return (sum, length);
    }

    public (T sum, int length) Add(T value)
    {
        values_[current_] = value;
        current_ = (current_ + 1) % values_.Length;
        return Update();
    }
}
