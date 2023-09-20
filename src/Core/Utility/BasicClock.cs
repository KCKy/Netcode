using System;
using Serilog;

namespace Core.Utility;

public sealed class BasicClock : IClock
{
    public required TimeSpan Period { get; init; }

    /// </inheritdoc>
    public event Action? OnTick;

    ILogger logger = Log.ForContext<BasicClock>();

    /// </inheritdoc>
    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        while (true)
        {
            var delay = Task.Delay(Period, cancelToken);

            cancelToken.ThrowIfCancellationRequested();

            OnTick?.Invoke();

            if (delay.IsCompletedSuccessfully)
                logger.Error("Tick handling took longer than the tick period.");

            await delay;
        }
    }
}
