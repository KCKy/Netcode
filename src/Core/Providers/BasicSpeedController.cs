using Core.Utility;
using Serilog;

namespace Core.Providers;

public class BasicSpeedController : ISpeedController
{
    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        double period = 0;

        while (true)
        {
            logger_.Verbose("Clock waiting for {Period} seconds.", period);
            Task delay = Task.Delay(TimeSpan.FromSeconds(period), cancelToken);
            
            lock (mutex_)
                period = currentPeriod_;

            await delay;

            OnTick?.Invoke();
        }
    }

    double currentPeriod_ = 0;
    double targetDelta_ = 0;
    double currentDelta_ = 0;
    double targetSpeed_ = 1;

    public double SmoothingTime { get; init; } = 1;

    readonly ILogger logger_ = Log.ForContext<BasicSpeedController>();
    readonly object mutex_ = new();

    void Update()
    {
        // We need to catch up / lose D seconds over the time of one second.
        double difference =  targetDelta_ - currentDelta_;
        double deltaSpeed = targetSpeed_ * difference / SmoothingTime;

        double newSpeed = Math.Max(0, deltaSpeed + targetSpeed_);

        // The the update period in accordance to new speed.
        currentPeriod_ = Math.Min(1 / newSpeed, 1);

        logger_.Verbose("Setting new speed to {currentPeriod_} TPS. (D : {Difference}, V: {DeltaV})", newSpeed, difference, deltaSpeed);
    }

    public double TargetTPS
    {
        get
        {
            lock (mutex_)
                return targetSpeed_;
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
                targetSpeed_ = value;
                Update();
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
                average_ = new(5, value);
                Update();
            }
        }
    }
    
    MovingAverage<double> average_ = new(5);

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
                (double sum, int length) = average_.Add(value);
                currentDelta_ = sum / length;
                Update();
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
