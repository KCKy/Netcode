﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.Timing;
using Xunit.Abstractions;

namespace Kcky.GameNewt.Tests;

/// <summary>
/// Tests for <see cref="ThreadClock"/>
/// </summary>
public sealed class ClockTests
{
    sealed class ClockStats
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

    ITestOutputHelper output_;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="output">Output for logging.</param>
    public ClockTests(ITestOutputHelper output)
    {
        output_ = output;
    }

    /// <summary>
    /// Statistically test the clocks performance metrics (mean period, mean deviation of the period).
    /// </summary>
    /// <param name="time">The length of time to run the test for.</param>
    /// <param name="tps">The target ticks per second of the clock.</param>
    /// <param name="meanError">Allowed error in the mean clock period in seconds.</param>
    /// <param name="deviationError">Allowed error in the deviation of the clock period in seconds.</param>
    /// <returns>Task of the test.</returns>
    /// <remarks>
    /// This is not a unit test as it's outcome is undeterministic and dependent on external factors. Nonetheless, it is useful to assure functionality of the clock.
    /// </remarks>
    [Theory]
    [InlineData(5d, 100, 0.001, 0.0005)]
    [InlineData(5d, 20, 0.001, 0.0005)]
    [InlineData(5d, 40, 0.001, 0.0005)]
    [InlineData(10d, 2, 0.001, 0.0005)]
    public async Task Basic(float time, float tps, double meanError, double deviationError)
    {
        ThreadClock clock = new()
        {
            TargetTps = tps
        };

        double period = 1 / tps;

        ClockStats stats = new();

        clock.OnTick += stats.Tick;

        CancellationTokenSource cancellation = new();

        stats.Start();
        Task clockTask = clock.RunAsync(cancellation.Token);
        
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

        output_.WriteLine($"Mean: {meanDelta} Deviation: {deltaDeviation}");
        Assert.InRange(meanDelta, period - meanError, period + meanError);
        Assert.InRange(deltaDeviation, -deviationError, deviationError);
    }
}
