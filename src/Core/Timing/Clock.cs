using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kcky.Useful;

namespace Kcky.GameNewt.Timing;

/// <summary>
/// A clock which periodically raises an event.
/// Expects <see cref="Update"/> to be called often to check for clock ticks.
/// </summary>
sealed class Clock : IClock
{
    /// <inheritdoc/>
    public event Action? OnTick;
    
    long targetPeriod_;
    double targetTps_;
    long last_;

    CancellationToken cancelToken_ = new(true);

    /// <inheritdoc/>
    public double TargetTps
    {
        get => targetTps_;
        set
        {
            double period = 1 / value * Stopwatch.Frequency;
            targetPeriod_ = (long)Math.Clamp(period, 1, long.MaxValue);
            targetTps_ = value;
        }
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">If the given platform does not support precise hardware timing required by the clock.</exception>
    public Clock()
    {
        if (!Stopwatch.IsHighResolution)
            throw new PlatformNotSupportedException("Platform does not support precision timing.");
        TargetTps = 1;
    }
    
    /// <summary>
    /// The update method of the clock.
    /// Should be called regularly to check for clock ticks.
    /// <returns>The time the next clock tick will be in.</returns>
    /// </summary>
    public long Update()
    {
        if (cancelToken_.IsCancellationRequested)
            return long.MaxValue;

        while (true)
        {
            long current = Stopwatch.GetTimestamp();
            long delta = current - last_;
            long remaining = targetPeriod_ - delta;

            if (remaining > 0)
                return remaining;
            
            last_ = current + remaining;

            OnTick?.Invoke();
        }
    }

    /// <summary>
    /// Starts the clock.
    /// </summary>
    /// <param name="cancelToken">Token to stop the clock from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the clock lifetime.</returns>
    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        last_ = Stopwatch.GetTimestamp();
        cancelToken_ = cancelToken;

        await cancelToken;
        throw new OperationCanceledException();
    }
}
