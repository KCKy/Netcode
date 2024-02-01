using System.Numerics;

namespace Useful;

/// <summary>
/// Used for calculating the minimum of last <see cref="Length"/> entries in a series.
/// </summary>
/// <typeparam name="T">The value to keep track of.</typeparam>
public struct MinStatsWindow<T> where T : struct, IComparisonOperators<T, T, bool>, IMinMaxValue<T>
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
    /// <exception cref="ArgumentOutOfRangeException">If <see cref="length"/> is non-positive.</exception>
    public MinStatsWindow(int length)
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

public struct AvgIntegrateStatsResetting
{
    double last_ = float.NaN;
    double sum_ = 0;
    double weight_ = 0;

    public AvgIntegrateStatsResetting() { }
    public double ResetTimeSeconds { get; init; } = 1;

    public double Update(double delta, double amount)
    {
        sum_ += amount * delta;
        weight_ += delta;

        if (weight_ > ResetTimeSeconds)
        {
            last_ = sum_ / weight_;
            sum_ = 0;
            weight_ = 0;
        }

        return last_;
    }
}

public struct AvgCountStatsResetting
{
    double last_ = float.NaN;
    double sum_ = 0;
    double weight_ = 0;
    long count_ = 0;

    public AvgCountStatsResetting() { }
    public double ResetTimeSeconds { get; init; } = 1;

    public double Update(double delta, double amount)
    {
        sum_ += amount;
        count_++;
        weight_ += delta;

        if (weight_ > ResetTimeSeconds)
        {
            last_ = sum_ / count_;
            sum_ = 0;
            count_ = 0;
            weight_ = 0;
        }

        return last_;
    }
}
