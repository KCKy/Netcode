using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Dispatcher;
using Kcky.GameNewt.Timing;
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
    readonly float targetTps_;

    /// <summary>
    /// The desired number of clock ticks per second.
    /// </summary>
    /// <remarks>
    /// May be momentarily modified to resynchronize the clock.
    /// </remarks>
    public float TargetTps
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
        set => normalizedDelays_ = new(value);
    }

    /// <summary>
    /// Constructor.
    /// <param name="clock">IClock instance to use for tick measuring.</param>
    /// <param name="loggerFactory">Logger factory to construct loggers for this instance and internal types.</param>
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
    public float CurrentTps { get; private set; } = 0;

    /// <summary>
    /// The desired delay between the server receiving input and the corresponding input collection.
    /// </summary>
    public float TargetDelta { get; set; } = 0;

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
            logger_.LogDebug("Started sync-clock.");
            logger_.LogTrace("Processing first clock tick.");
            long currentTime = Stopwatch.GetTimestamp();
            OnTick?.Invoke();
            beginTime_ = currentTime;
            timingQueue_.Add(currentTime);
            Debug.Assert(beginFrame_ == timingQueue_.FirstIndex);
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

    IntegrateWindowed<float> normalizedDelays_ = new(20)
    {
        Statistic = static values => values.Min()
    };

    float GetOffset(long targetFrame, long targetTime, long sourceFrame, long sourceTime)
    {
        float supposedTime = (targetFrame - sourceFrame) / TargetTps;
        float actualTime = (targetTime - sourceTime) /  (float) Stopwatch.Frequency;
        return supposedTime - actualTime;
    }

    float GetNormalizationOffset(long frame, long time) => GetOffset(beginFrame_, beginTime_, frame, time);
    
    float GetDenormalizationOffset()
    {
        long latestFrame = timingQueue_.LastIndex;
        long latestFrameTime = timingQueue_.Last;

        return GetOffset(latestFrame, latestFrameTime, beginFrame_, beginTime_);
    }

    float currentWorstCase_ = 0;

    void UpdateClockSpeed()
    {
        float offset = GetDenormalizationOffset();
        float denormalizedWorstCase = currentWorstCase_ + offset - TargetDelta;
        float newPeriod = Math.Max(1 / TargetTps + denormalizedWorstCase, 0);

        logger_.LogTrace("Updating clock speed from current worst case {WorstCase} with denormalization offset {Offset} yields period {Period}.", currentWorstCase_, offset, newPeriod);

        float newTps = 1 / newPeriod;
        CurrentTps = newTps;
        internalClock_.TargetTps = newTps;
    }

    /// <summary>
    /// Inform the clock about a measured delay from the distant clock for a given frame.
    /// Ideal delay is zero.
    /// For more see <see cref="SetDelayDelegate"/>.
    /// </summary>
    /// <param name="frame">The frame the delay belongs to.</param>
    /// <param name="delay">The delay amount. Negative value means the clock should catch up.</param>
    public void SetDelay(long frame, float delay)
    {
        lock (mutex_)
        {
            logger_.LogTrace("Updating delay with frame {Frame} delay {Delay}.", frame, delay);

            if (!timingQueue_.TryGet(frame, out long time))
                return;

            timingQueue_.Pop(frame - 1);

            float offset = GetNormalizationOffset(frame, time);
            float normalizedDelay = delay + offset;
            currentWorstCase_ = normalizedDelays_.Add(normalizedDelay);

            logger_.LogTrace("Normalization offset {Offset} yields normalized delay {Delay} and current worst case {Case}.", offset, normalizedDelay, currentWorstCase_);
            
            UpdateClockSpeed();
        }
    }

    void TickHandler()
    {
        logger_.LogTrace("Processing regular clock tick.");
        long currentTime = Stopwatch.GetTimestamp();
        OnTick?.Invoke();
        
        lock (mutex_)
        {
            timingQueue_.Add(currentTime);
            UpdateClockSpeed();
        }
    }
}
