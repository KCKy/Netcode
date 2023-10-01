using Serilog;
using Serilog.Core;

namespace Core.Providers;

public class BasicSpeedController : ISpeedController
{
    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        double period;

        lock (mutex_)
            period = currentPeriod_;

        while (true)
        {
            Task delay = Task.Delay(TimeSpan.FromSeconds(period), cancelToken);
            
            lock (mutex_)
                period = currentPeriod_;

            await delay;

            OnTick?.Invoke();
        }
    }

    void UpdateSpeed()
    {
        if (Math.Abs(targetDelta_ - currentDelta_) < SafeMargin)
        {
            logger_.Verbose("Keeping normal speed.");
            currentPeriod_ = targetPeriod_;
        }
        else
        {
            if (targetDelta_ > currentDelta_)
            {
                logger_.Verbose("Running faster.");
                currentPeriod_ = targetPeriod_ / SpeedUp;
            }
            else
            {
                logger_.Verbose("Running slower.");
                currentPeriod_ = targetPeriod_ / SpeedDown;
            }
        }
    }

    public double SafeMargin { get; init; } = 0.05;
    public double SpeedUp { get; init; } = 1.1;
    public double SpeedDown { get; init; } = 0.95;

    double currentPeriod_ = 0;
    double targetPeriod_ = 1;
    double targetDelta_ = 0;
    double currentDelta_ = 0;

    readonly ILogger logger_ = Log.ForContext<BasicSpeedController>();

    readonly object mutex_ = new();

    double targetTps_ = 1;

    public double TargetTPS
    {
        get
        {
            lock (mutex_)
                return targetTps_;
        }
        set
        {
            if (!double.IsPositive(value))
            {
                logger_.Fatal("Got non-positive {TPS}.", value);
                throw new ArgumentOutOfRangeException(nameof(value), value, "TPS must be a positive number.");
            }

            lock (mutex_)
            {
                targetTps_ = value;
                targetPeriod_ = 1 / value;
                UpdateSpeed();
            }
        }
    }

    const string DeltaMustBeReal = "Delta must be a real number.";

    public double TargetDelta
    {
        get => targetDelta_;
        set
        {
            if (!double.IsRealNumber(value))
            {
                logger_.Fatal("Got non-real {TargetDelta}.", value);
                throw new ArgumentOutOfRangeException(nameof(value), value, DeltaMustBeReal);
            }

            lock (mutex_)
            {
                targetDelta_ = value;
                UpdateSpeed();
            }
        }
    }

    public double CurrentDelta
    {
        get => currentDelta_;
        set
        {
            if (!double.IsRealNumber(value))
            {
                logger_.Fatal("Got non-real {CurrentDelta}.", value);
                throw new ArgumentOutOfRangeException(nameof(value), value, DeltaMustBeReal);
            }

            logger_.Verbose("Updated delta to {Delta}", value);

            lock (mutex_)
            {
                
                currentDelta_ = value;
                UpdateSpeed();
            }
        }
    }

    public double CurrentTPS
    {
        get
        {
            lock (mutex_)
                return 1f / currentPeriod_;
        }
    }
    
    public event Action? OnTick;
}
