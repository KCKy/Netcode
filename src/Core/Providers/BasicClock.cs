using Serilog;

namespace Core.Providers;

public sealed class BasicClock : IClock
{
    TimeSpan period_;

    /// </inheritdoc>
    public event Action? OnTick;

    double targetTps_ = 1;

    public double TargetTPS
    {
        get => targetTps_;
        set
        {
            targetTps_ = value;
            period_ = TimeSpan.FromSeconds(1d / value);
        }
    }

    readonly ILogger logger_ = Log.ForContext<BasicClock>();


    /// </inheritdoc>
    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        while (true)
        {
            var delay = Task.Delay(period_, cancelToken);

            cancelToken.ThrowIfCancellationRequested();

            OnTick?.Invoke();

            if (delay.IsCompletedSuccessfully)
                logger_.Error("Tick handling took longer than the tick period.");

            await delay;
        }
    }
}
