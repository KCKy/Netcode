using System.Diagnostics;
using Core.Providers;
using Serilog;
using Xunit.Abstractions;

namespace CoreTests;

public class ClockTests
{
    class ClockStats
    {
        readonly object mutex_ = new();

        long ticks_ = 0;
        long lastTime_ = 0;
        double deltaSum_ = 0;
        double deltaSquaredSum_ = 0;
        
        public (double meanDelta, double deltaDeviation) GetStats()
        {
            lock (mutex_)
            {
                double mean = deltaSum_ / ticks_;
                double variance = deltaSquaredSum_ / ticks_ - mean * mean;
                double deviation = Math.Sqrt(variance);
                return (mean, deviation);
            }
        }

        public void Start()
        {
            long stamp = Stopwatch.GetTimestamp();

            lock (mutex_)
                lastTime_ = stamp;
        }

        public void Tick()
        {
            long stamp = Stopwatch.GetTimestamp();

            lock (mutex_)
            {
                double delta = Stopwatch.GetElapsedTime(lastTime_, stamp).TotalSeconds;

                lastTime_ = stamp;
                ticks_++;
                
                deltaSum_ += delta;
                deltaSquaredSum_ += delta * delta;
            }
        }
    }

    public ClockTests(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.TestOutput(output).MinimumLevel.Debug().CreateLogger();
    }

    [Theory]
    [InlineData(5d, 0.01, 0.001, 0.0005)]
    [InlineData(5d, 0.05, 0.001, 0.0005)]
    [InlineData(5d, 0.025, 0.001, 0.0005)]
    [InlineData(10d, 0.5, 0.001, 0.0005)]
    public async Task Basic(double time, double expectedMean, double meanError, double deviationError)
    {
        Clock clock = new()
        {
            TargetTPS = 1 / expectedMean
        };

        ClockStats stats = new();

        clock.OnTick += stats.Tick;

        CancellationTokenSource cancellation = new();

        Task clockTask = clock.RunAsync(cancellation.Token);
        stats.Start();

        await Task.WhenAny(clockTask, Task.Delay(TimeSpan.FromSeconds(time), cancellation.Token));

        Assert.False(clockTask.IsCompleted);

        cancellation.Cancel();

        try
        {
            await clockTask;
        }
        catch (Exception) { }

        Assert.True(clockTask.IsCanceled);

        (double meanDelta, double deltaDeviation) = stats.GetStats();

        Assert.InRange(meanDelta, expectedMean - meanError, expectedMean + meanError);
        Assert.InRange(deltaDeviation, -deviationError, deviationError);

        Log.Information("Mean: {Mean} Deviation: {Deviation}", meanDelta, deltaDeviation);
    }
}
