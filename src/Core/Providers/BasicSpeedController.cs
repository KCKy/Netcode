using Serilog;
using Useful;

namespace Core.Providers;

public class BasicSpeedController : ISpeedController
{
    readonly BasicClock clock_ = new();

    public Task RunAsync(CancellationToken cancelToken = new()) => clock_.RunAsync(cancelToken);

    public event Action? OnTick
    {
        add => clock_.OnTick += value;
        remove => clock_.OnTick -= value;
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

        // The update period in accordance to new speed.
        currentPeriod_ = Math.Min(1 / newSpeed, 1);

        clock_.TargetTPS = CurrentTPS;

        logger_.Verbose("Setting new period to {currentPeriod_} s. (D : {Difference}, V: {DeltaV})", newSpeed, difference, deltaSpeed);
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
                Update();
            }
        }
    }
    
    DelayStats<double> stats_ = new(5); // TODO: make this mutable

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

            lock (mutex_)
            {
                currentDelta_ = stats_.Add(value);
                logger_.Verbose("Updated delta to {Delta}", currentDelta_);
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
}
