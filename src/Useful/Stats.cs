using System.Numerics;

namespace Useful;

/// <summary>
/// Used for calculating the minimum of last <see cref="Length"/> entries in a series.
/// </summary>
/// <typeparam name="T">The type of value, which will be kept track of.</typeparam>
public struct MinimumWindowed<T> where T : struct, IComparisonOperators<T, T, bool>, IMinMaxValue<T>
{
    readonly Queue<T> queue_;

    /// <summary>
    /// Length of the computing window.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="length">The length of the window to calculate minimum from.</param>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="length"/> is non-positive.</exception>
    public MinimumWindowed(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "The length of the window must be positive.");

        Length = length;
        queue_ = new(length);
    }

    /// <summary>
    /// Add next value from the series.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <returns>The current minimum from the latest window of size <see cref="Length"/>.</returns>
    public T Add(T value)
    {
        queue_.Enqueue(value);
        if (queue_.Count > Length)
            queue_.Dequeue();
        
        return queue_.Min();
    }
}

/// <summary>
/// Calculates time weighted average of a value.
/// It is calculated over a period of length <see cref="ResetTime"/> after that the collection is reset and a new average starts being calculated.
/// </summary>
public struct TimeWeightedAverage
{
    double last_ = float.NaN;
    double sum_ = 0;
    double weight_ = 0;

    /// <summary>
    /// Constructor.
    /// </summary>
    public TimeWeightedAverage() { }

    /// <summary>
    /// The length of the averaged period.
    /// </summary>
    public double ResetTime { get; init; } = 1;

    /// <summary>
    /// Update the average calculation.
    /// </summary>
    /// <param name="delta">Time passed since the last update.</param>
    /// <param name="amount">The amount to be recorded for this update.</param>
    /// <returns>Time weighted average of the last completed period.</returns>
    public double Update(double delta, double amount)
    {
        sum_ += amount * delta;
        weight_ += delta;

        if (weight_ > ResetTime)
        {
            last_ = sum_ / weight_;
            sum_ = 0;
            weight_ = 0;
        }

        return last_;
    }
}

/// <summary>
/// Calculates an average for a value.
/// It is calculated over a period of length <see cref="ResetTime"/> after that the collection is reset and a new average starts being calculated.
/// </summary>
public struct Average
{
    double last_ = float.NaN;
    double sum_ = 0;
    double weight_ = 0;
    long count_ = 0;

    /// <summary>
    /// Constructor.
    /// </summary>
    public Average() { }

    /// <summary>
    /// The length of the averaged period.
    /// </summary>
    public double ResetTime { get; init; } = 1;

    /// <summary>
    /// Update the average calculation.
    /// </summary>
    /// <param name="delta">Time passed since the last update.</param>
    /// <param name="amount">The amount to be recorded for this update.</param>
    /// <returns>Average of the last completed period.</returns>
    public double Update(double delta, double amount)
    {
        sum_ += amount;
        count_++;
        weight_ += delta;

        if (weight_ > ResetTime)
        {
            last_ = sum_ / count_;
            sum_ = 0;
            count_ = 0;
            weight_ = 0;
        }

        return last_;
    }
}
