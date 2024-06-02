using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Timing;

/// <summary>
/// A clock which periodically raises an event.
/// Creates a timing thread, which uses passive waiting.
/// </summary>
sealed class ThreadClock : IClock
{
    /// <inheritdoc/>
    public event Action OnTick
    {
        add => clock_.OnTick += value;
        remove => clock_.OnTick -= value;
    }

    readonly Clock clock_ = new();
    static readonly long TicksPerMs = Stopwatch.Frequency / 1000;
    long clockQuantumTicks_ = TicksPerMs * 15;

    /// <summary>
    /// The smallest time period of sleep to be requested from the OS.
    /// </summary>
    /// <remarks>
    /// On some operating systems (like sleep) calling sleep on a too small value results in sleeping for an undesirably long amount of time.
    /// In these cases it is desirable to yield instead.
    /// </remarks>
    public TimeSpan ClockQuantum
    {
        get => TimeSpan.FromSeconds(clockQuantumTicks_ / (float)Stopwatch.Frequency);
        set => clockQuantumTicks_ = (long)(value.TotalSeconds * Stopwatch.Frequency);
    }

    /// <inheritdoc/>
    public float TargetTps
    {
        get => clock_.TargetTps;
        set => clock_.TargetTps = value;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">If the given platform does not support precise hardware timing required by the clock.</exception>
    public ThreadClock()
    {
        if (!Stopwatch.IsHighResolution)
            throw new PlatformNotSupportedException("Platform does not support precision timing.");
    }

    void TimerThread(object? cancelTokenRaw)
    {
        CancellationToken cancelToken = (CancellationToken)cancelTokenRaw!;

        while (true)
        {
            long remaining = clock_.Update();

            if (cancelToken.IsCancellationRequested)
                return;

            long wait = Math.Max(remaining - clockQuantumTicks_, 0);
            Thread.Sleep((int)(wait / TicksPerMs));
        }
    }

    /// <summary>
    /// Starts the clock.
    /// </summary>
    /// <param name="cancelToken">Token to stop the clock from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the clock lifetime.</returns>
    public Task RunAsync(CancellationToken cancelToken)
    {
        Task clockTask = clock_.RunAsync(cancelToken);
        Thread thread = new(TimerThread)
        {
            Priority = ThreadPriority.Highest,
            IsBackground = true
        };
        thread.Start(cancelToken);
        return clockTask;
    }
}
