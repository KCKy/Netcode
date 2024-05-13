using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Timing;
using Kcky.GameNewt.Transport;
using Kcky.Useful;
using Microsoft.Extensions.Logging;

namespace GameNewt.Timing;

/// <summary>
/// A clock which keeps itself synchronized to a distant clock.
/// </summary>
sealed class SynchronizedClock
{
    readonly object mutex_ = new();
    readonly IClock internalClock_;
    readonly ILogger logger_;
    readonly double targetTps_;

    /// <summary>
    /// The desired number of clock ticks per second.
    /// </summary>
    /// <remarks>
    /// May be momentarily modified to resynchronize the clock.
    /// </remarks>
    public double TargetTps
    {
        get => targetTps_;
        init
        {
            targetTps_ = value;
            internalClock_.TargetTps = value;
        }
    }

    /// <summary>
    /// To account for jitter the clock works over a window of latencies.
    /// This value determines the number of frames for this window.
    /// </summary>
    public int SamplingWindow
    {
        get => normalizedDelays_.Length;
        init => normalizedDelays_ = new(value);
    }

    /// <summary>
    /// Constructor.
    /// <param name="clock">IClock instance to use for tick measuring.</param>
    /// </summary>
    public SynchronizedClock(IClock clock, ILoggerFactory loggerFactory)
    {
        logger_ = loggerFactory.CreateLogger<SynchronizedClock>();
        internalClock_ = clock;
        internalClock_.OnTick += TickHandler;
        TargetTps = 1;
    }

    /// <summary>
    /// Called on each clock tick.
    /// </summary>
    public event Action? OnTick;

    long beginFrame_;
    long beginTime_;

    /// <summary>
    /// The current TPS of the clock.
    /// </summary>
    public double CurrentTps { get; private set; } = 0;
    
    /// <summary>
    /// Initialize the clock.
    /// </summary>
    /// <param name="frame">The frame the clock should be at, i.e. the next tick would be for frame + 1.</param>
    public void Initialize(long frame)
    {
        lock (mutex_)
        {
            logger_.LogDebug("Initialized sync-clock with frame {Frame}.", frame);
            beginFrame_ = frame + 1;
            timingQueue_.Set(frame + 1);
        }
    }

    /// <summary>
    /// Start the clock.
    /// <see cref="Initialize"/> needs to be called beforehand.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        lock (mutex_)
        {
            logger_.LogDebug("ServerStarted sync-clock.");
            beginTime_ = Stopwatch.GetTimestamp();
            TickHandlerUnsafe();
        }

        try
        {
            await internalClock_.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            internalClock_.OnTick -= TickHandler;
            throw;
        }
    }

    readonly IndexedQueue<long> timingQueue_ = new();

    readonly IntegrateWindowed<double> normalizedDelays_ = new(20)
    {
        Statistic = static values => values.Min()
    };


    double GetOffset(long targetFrame, long targetTime, long sourceFrame, long sourceTime)
    {
        double supposedTime = (targetFrame - sourceFrame) / TargetTps;
        double actualTime = (targetTime - sourceTime) /  (double) Stopwatch.Frequency;
        return supposedTime - actualTime;
    }

    double GetOffset(double targetFrame, long targetTime, double sourceFrame, long sourceTime)
    {
        double supposedTime = (targetFrame - sourceFrame) / TargetTps;
        double actualTime = (targetTime - sourceTime) / (double) Stopwatch.Frequency;
        return supposedTime - actualTime;
    }

    double GetNormalizationOffset(long frame, long time) => GetOffset(beginFrame_, beginTime_, frame, time);
    
    double CalculateCurrentFrameProgression(long frameStartTime, long currentTime)
    {
        return (currentTime - frameStartTime) * internalClock_.TargetTps / Stopwatch.Frequency;
    }

    double GetDenormalizationOffset(long currentTime)
    {
        long latestFrame = timingQueue_.LastIndex;
        long latestFrameTime = timingQueue_.Last;
        double currentFrame = latestFrame + CalculateCurrentFrameProgression(latestFrameTime, currentTime);

        return GetOffset(currentFrame, currentTime, beginFrame_, beginTime_);
    }

    double currentWorstCase_ = 0;

    void UpdateClockSpeed(long currentTime)
    {
        double offset = GetDenormalizationOffset(currentTime);
        double denormalizedWorstCase = currentWorstCase_ + offset;
        double newPeriod = Math.Max(1 / TargetTps + denormalizedWorstCase, 0);

        logger_.LogTrace("Updating clock speed with denormalization offset {Offset} yields period {Period}.", offset, newPeriod);

        if (newPeriod <= 0)
        {
            TickHandlerUnsafe();
        }
        else
        {
            double newTps = 1 / newPeriod;
            CurrentTps = newTps;
            internalClock_.TargetTps = newTps;
        }
    }

    /// <summary>
    /// Inform the clock about a measured delay from the distant clock for a given frame.
    /// Ideal delay is zero.
    /// For more see <see cref="SetDelayDelegate"/>.
    /// </summary>
    /// <param name="frame">The frame the delay belongs to.</param>
    /// <param name="delay">The delay amount. Negative value means the clock should catch up.</param>
    public void SetDelayHandler(long frame, double delay)
    {
        lock (mutex_)
        {
            logger_.LogTrace("Updating delay with frame {Frame} delay {Delay}.", frame, delay);

            if (!timingQueue_.TryGet(frame, out long time))
                return;

            timingQueue_.Pop(frame - 1);

            double offset = GetNormalizationOffset(frame, time);
            double normalizedDelay = delay + offset;
            currentWorstCase_ = normalizedDelays_.Add(normalizedDelay);

            logger_.LogTrace("Normalization offset {Offset} yields normalized delay {Delay} and current worst case {Case}.", offset, normalizedDelay, currentWorstCase_);
            
            long currentTime = Stopwatch.GetTimestamp();
            UpdateClockSpeed(currentTime);
        }
    }

    void TickHandler()
    {
        long currentTime = Stopwatch.GetTimestamp();
        logger_.LogTrace("Processing regular clock tick.");
        OnTick?.Invoke();
        
        lock (mutex_)
        {
            timingQueue_.Add(currentTime);
            UpdateClockSpeed(currentTime);
        }
    }

    void TickHandlerUnsafe()
    {
        long currentTime = Stopwatch.GetTimestamp();
        logger_.LogTrace("Processing fake clock tick.");
        OnTick?.Invoke();
        timingQueue_.Add(currentTime);
        UpdateClockSpeed(currentTime);
    }
}
