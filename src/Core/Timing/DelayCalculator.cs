using Core.DataStructures;
using System;
using System.Diagnostics;
using System.Threading;
using Serilog;

namespace Core.Timing;

sealed class DelayCalculator<TG, TC, TS>
    where TG : IGameState<TC, TS>
    where TC : class, new()
    where TS : class, new()
{
    readonly object mutex_ = new();
    readonly IndexedQueue<long> timingQueue_ = new();
    readonly double tpsReciprocal_ = 1 / TG.DesiredTickRate;
    readonly ILogger logger_ = Log.ForContext<DelayCalculator<TG, TC, TS>>();
    public required ISpeedController UsedSpeedController { get; set; }

    double CalculateCurrentFrameProgression(long frameStartTime, long currentTime, double currentTps)
    {
        return (currentTime - frameStartTime) * currentTps / Stopwatch.Frequency;
    }

    double? CalculateDelayOffset(long originalFrame, long currentTime, double tps)
    {
        if (!timingQueue_.TryGet(originalFrame, out long originalFrameTime))
            return null;

        long latestFrame = timingQueue_.LastIndex;
        long latestFrameTime = timingQueue_.Last;
        
        timingQueue_.Pop(originalFrame - 1);

        double currentFrame = latestFrame + CalculateCurrentFrameProgression(latestFrameTime, currentTime, tps);

        double supposedTime = (currentFrame - originalFrame) * tpsReciprocal_; // The time it would take the server
        double actualTime = (currentTime - originalFrameTime) / (double)Stopwatch.Frequency; // The time it actually took the client to this point
        double offset = supposedTime - actualTime;

        logger_.Verbose("Supposed time: {Supposed}, Actual time {Actual}.", supposedTime, actualTime);

        return offset;
    }

    long latestDelayFrame_ = long.MinValue;
    double latestDelay_ = 0;

    record UpdateContext(long Current, double Tps);

    UpdateContext GetUpdateContext()
    {
        long stamp = Stopwatch.GetTimestamp();
        double tps = UsedSpeedController.CurrentTps;
        return new(stamp, tps);
    }
    
    void UpdateAndExitLock(UpdateContext ctx)
    {
        double delay = latestDelay_;

        if (CalculateDelayOffset(latestDelayFrame_, ctx.Current, ctx.Tps) is not { } offset)
        {
            Monitor.Exit(mutex_);
            return;
        }

        double newDelay = latestDelay_ + offset;

        Monitor.Exit(mutex_);
        
        logger_.Verbose("Updating delay to server to {Delay} + {Offset} = {Sum}.", delay, offset, newDelay);
        OnDelayChanged?.Invoke(newDelay);
    }

    public event Action<double>? OnDelayChanged;

    public void Init(long frame)
    {
        lock (mutex_)
            timingQueue_.Set(frame + 1);
    }

    public void Update()
    {
        UpdateContext ctx = GetUpdateContext();
        Monitor.Enter(mutex_);
        UpdateAndExitLock(ctx);
    }

    public void SetDelay(long frame, double delay)
    {
        UpdateContext ctx = GetUpdateContext();

        Monitor.Enter(mutex_);

        if (frame <= latestDelayFrame_)
            return;
        
        latestDelay_ = delay;
        latestDelayFrame_ = frame;

        UpdateAndExitLock(ctx);

        logger_.Verbose("Received new delay info {Frame}: {Delay}.", frame, delay);
    }

    public long Tick()
    {
        UpdateContext ctx = GetUpdateContext();

        Monitor.Enter(mutex_);

        long id = timingQueue_.Add(ctx.Current);

        UpdateAndExitLock(ctx);

        logger_.Verbose("New tick occured.");

        return id;
    }
}
