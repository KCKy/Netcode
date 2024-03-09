using Core.DataStructures;
using System;
using System.Diagnostics;

namespace Core.Timing;

sealed class DelayCalculator<TGameState, TC, TS>
    where TGameState : IGameState<TC, TS>
    where TC : class, new()
    where TS : class, new()
{
    readonly IndexedQueue<long> timingQueue_ = new();
    readonly double tpsReciprocal_ = 1 / TGameState.DesiredTickRate;

    double? CalculateDelayOffset(long originalFrame)
    {
        long originalFrameTime, latestFrameTime, latestFrame;

        lock (timingQueue_)
        {
            if (!timingQueue_.TryGet(originalFrame, out originalFrameTime))
                return null;

            latestFrame = timingQueue_.LastIndex;
            latestFrameTime = timingQueue_.Last;
            
            timingQueue_.Pop(originalFrame - 1);
        }

        double supposedTime = (latestFrame - originalFrame) * tpsReciprocal_; // The time it would take the server
        double actualTime = (latestFrameTime - originalFrameTime) / (double)Stopwatch.Frequency; // The time it actually took the client
        double offset = supposedTime - actualTime;

        return offset;
    }

    readonly object delayLock_ = new();
    long latestDelayFrame_ = long.MinValue;
    double latestDelay_ = 0;
    
    (long frame, double delay) GetDelay()
    {
        lock (delayLock_)
            return (latestDelayFrame_, latestDelay_);
    }

    void Update()
    {
        (long frame, double delay) = GetDelay();
        if (CalculateDelayOffset(frame) is { } offset)
            OnDelayChanged?.Invoke(delay + offset);
    }

    public event Action<double>? OnDelayChanged;

    public void Init(long frame)
    {
        lock (timingQueue_)
            timingQueue_.Set(frame + 1);
    }
    
    public void SetDelay(long frame, double delay)
    {
        lock (delayLock_)
        {
            if (frame <= latestDelayFrame_)
                return;

            latestDelay_ = delay;
            latestDelayFrame_ = frame;
        }

        Update();
    }

    public long Tick()
    {
        long stamp = Stopwatch.GetTimestamp();

        long id;
        lock(timingQueue_)
            id = timingQueue_.Add(stamp);

        Update();
        return id;
    }
}
