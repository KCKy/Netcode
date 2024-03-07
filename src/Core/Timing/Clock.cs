using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Useful;

namespace Core.Timing;

/// <summary>
/// A clock which periodically raises an event.
/// Creates a timing thread, which uses passive waiting.
/// </summary>
public sealed class Clock
{
    /// <summary>
    /// Is evenly called on a clock tick. Provides delta time in seconds.
    /// </summary>
    public event Action? OnTick;
    
    volatile int targetPeriod_;
    double targetTps_;
    static readonly long TicksPerMs = Stopwatch.Frequency / 1000;
    readonly long clockQuantumTicks_ = TicksPerMs * 15;

    /// <summary>
    /// The smallest time period of sleep to requested from the OS.
    /// </summary>
    /// <remarks>
    /// On some operating systems (like sleep) calling sleep on a too small value results in sleeping for an undesirably long amount of time.
    /// In these cases it is desirable to yield instead.
    /// </remarks>
    public TimeSpan ClockQuantumSecs
    {
        get => TimeSpan.FromSeconds(clockQuantumTicks_ / (double)Stopwatch.Frequency);
        init => clockQuantumTicks_ = (long)(value.TotalSeconds * Stopwatch.Frequency);
    }

    
    readonly long maxWaitTime_ = TicksPerMs * 100;

    /// <summary>
    /// The smallest time period of sleep to requested from the OS.
    /// </summary>
    /// <remarks>
    /// On some operating systems (like sleep) calling sleep on a too small value results in sleeping for an undesirably long amount of time.
    /// In these cases it is desirable to yield instead.
    /// </remarks>
    public TimeSpan MaxWaitTime
    {
        get => TimeSpan.FromSeconds(maxWaitTime_ / (double)Stopwatch.Frequency);
        init => maxWaitTime_ = (long)(value.TotalSeconds * Stopwatch.Frequency);
    }

    /// <summary>
    /// The target TPS of the clock, the clock should as closely match this TPS while spacing the ticks evenly.
    /// </summary>
    public double TargetTps
    {
        get => targetTps_;
        set
        {
            targetPeriod_ = (int)(1 / value * Stopwatch.Frequency);
            logger_.Verbose("New clock target period: {Period} ({Freq} tps)", targetPeriod_, Stopwatch.Frequency);
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

    readonly ILogger logger_ = Log.ForContext<Clock>();

    void TimerThread(object? cancelTokenRaw)
    {
        CancellationToken cancelToken = (CancellationToken)cancelTokenRaw!;

        long tickSum = 0;
        long tickCount = 0;

        long last = Stopwatch.GetTimestamp();

        while (true)
        {
            long current = Stopwatch.GetTimestamp();
            long delta = current - last;
            long period = targetPeriod_;

            if (cancelToken.IsCancellationRequested)
                return;

            long remaining = period - delta;

            if (remaining <= 0)
            {
                last = current + remaining;

                {
                    double freq = Stopwatch.Frequency;
                    tickSum += delta;
                    tickCount++;

                    logger_.Verbose("Tick took {Time:F5} s (Avg: {Avg:F5} s).", delta / freq, (double)tickSum / tickCount / freq);

                    OnTick?.Invoke();
                }

                continue;
            }

            long wait = Math.Clamp(remaining - clockQuantumTicks_, 0, maxWaitTime_);
            Thread.Sleep((int)(wait / TicksPerMs));
        }
    }

    /// <summary>
    /// Starts the clock.
    /// </summary>
    /// <param name="cancelToken">Token to stop the clock from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the clock lifetime.</returns>
    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        Thread thread = new(TimerThread)
        {
            Priority = ThreadPriority.Highest
        };
        thread.Start(cancelToken);
        await cancelToken;
        throw new OperationCanceledException();
    }
}
