using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Timing;
using Kcky.Useful;
using Serilog;

namespace GameNewt.Timing;

sealed class SynchronizedClock
{
    readonly object mutex_ = new();
    readonly Clock internalClock_ = new();

    readonly ILogger logger_ = Log.ForContext<SynchronizedClock>();

    readonly double targetTps_;
    public double TargetTps
    {
        get => targetTps_;
        init
        {
            targetTps_ = value;
            internalClock_.TargetTps = value;
        }
    }

    public SynchronizedClock()
    {
        internalClock_.OnTick += TickHandler;
    }

    public event Action? OnTick;

    long beginFrame_;
    long beginTime_;

    public double CurrentTps { get; private set; } = 0;
    
    public void Initialize(long frame)
    {
        lock (mutex_)
        {
            logger_.Debug("Initialized sync-clock with frame {Frame}.", frame);
            beginFrame_ = frame + 1;
            timingQueue_.Set(frame + 1);
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        lock (mutex_)
        {
            logger_.Debug("Started sync-clock.");
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
        Statistic = static values => values.Max()
    };

    double GetNormalizationOffset(long frame, long time)
    {
        double supposedTime = (beginFrame_ - frame) / TargetTps;
        double actualTime = (beginTime_ - time) / (double)Stopwatch.Frequency;
        return supposedTime - actualTime;
    }

    double CalculateCurrentFrameProgression(long frameStartTime, long currentTime)
    {
        return (currentTime - frameStartTime) * internalClock_.TargetTps / Stopwatch.Frequency;
    }

    double GetDenormalizationOffset(long currentTime)
    {
        long latestFrame = timingQueue_.LastIndex;
        long latestFrameTime = timingQueue_.Last;

        double currentFrame = latestFrame + CalculateCurrentFrameProgression(latestFrameTime, currentTime);
        double supposedTime = (currentFrame - beginFrame_) / TargetTps;
        double actualTime = (currentTime - beginTime_) / (double)Stopwatch.Frequency;
        return supposedTime - actualTime;
    }

    double currentWorstCase_ = 0;

    void UpdateClockSpeed(long currentTime)
    {
        double offset = GetDenormalizationOffset(currentTime);
        double denormalizedWorstCase = currentWorstCase_ + offset;
        double newPeriod = Math.Max(1 / TargetTps + denormalizedWorstCase, 0);

        logger_.Verbose("Updating clock speed with denormalization offset {Offset} yields period {Period}.", offset, newPeriod);

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

    public void SetDelayHandler(long frame, double delay)
    {
        lock (mutex_)
        {
            logger_.Verbose("Updating delay with frame {Frame} delay {Delay}.", frame, delay);

            if (!timingQueue_.TryGet(frame, out long time))
                return;

            timingQueue_.Pop(frame - 1);

            double offset = GetNormalizationOffset(frame, time);
            double normalizedDelay = delay + offset;
            currentWorstCase_ = normalizedDelays_.Add(normalizedDelay);

            logger_.Verbose("Normalization offset {Offset} yields normalized delay {Delay} and current worst case {Case}.", offset, normalizedDelay, currentWorstCase_);
            
            long currentTime = Stopwatch.GetTimestamp();
            UpdateClockSpeed(currentTime);
        }
    }

    void TickHandler()
    {
        long currentTime = Stopwatch.GetTimestamp();
        logger_.Verbose("Processing regular clock tick.");
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
        logger_.Verbose("Processing fake clock tick.");
        OnTick?.Invoke();
        timingQueue_.Add(currentTime);
        UpdateClockSpeed(currentTime);
    }
}
