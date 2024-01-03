using Serilog;
using System.Diagnostics;
using Useful;

namespace Core.Providers;

public sealed class Clock : IClock
{
    /// </inheritdoc>
    public event Action? OnTick;
    
    volatile int targetPeriod_;
    double targetTps_;

    public double TargetTPS
    {
        get => targetTps_;
        set
        {
            targetPeriod_ = (int)(1 / value * Stopwatch.Frequency);
            logger_.Verbose("New clock target period: {Period} ({Freq} tps)", targetPeriod_, Stopwatch.Frequency);
            targetTps_ = value;
        }
    }

    public Clock()
    {
        if (!Stopwatch.IsHighResolution)
            throw new PlatformNotSupportedException("Platform does not support precision timing.");

        TargetTPS = 1;
    }

    readonly ILogger logger_ = Log.ForContext<Clock>();

    void TimerThread(object? _cancelToken)
    {
        CancellationToken cancelToken = (CancellationToken)_cancelToken!;

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

            if (delta >= period)
            {
                last = current - delta + period;

                {
                    double freq = Stopwatch.Frequency;
                    tickSum += delta;
                    tickCount++;
                    logger_.Verbose("Tick took {Time:F5} s (Avg: {Avg:F5} s).", delta / freq, (double)tickSum / tickCount / freq);

                    OnTick?.Invoke();
                }

                continue;
            }

            Thread.Yield();
        }
    }

    /// </inheritdoc>
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
