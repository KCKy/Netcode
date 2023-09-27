using Serilog;

namespace Core.Providers;

public class BasicSpeedController : ISpeedController
{
    readonly object mutex_ = new();

    public async Task RunAsync(CancellationToken cancelToken = new())
    {
        var delay = Task.Delay(TimeSpan.FromSeconds(currentPeriod_));
        
        // TODO: implement speed change

        await delay;

        // TODO: rewrite as a regular thread

        OnTick?.Invoke();
    }

    double currentPeriod_;
    double targetTps_;
    double targetDelta_;
    double currentDelta_;

    readonly ILogger logger_ = Log.ForContext<BasicSpeedController>();

    public double TargetTPS
    {
        get => targetTps_;
        set
        {
            if (!double.IsPositive(value))
            {
                logger_.Fatal("Got non-positive {TPS}.", value);
                throw new ArgumentOutOfRangeException(nameof(value), value, "TPS must be a positive number.");
            }
                
            currentPeriod_ = 1 / targetTps_;
            targetTps_ = value;
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

            targetDelta_ = value;
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

            currentDelta_ = value;
        }
    }

    public double CurrentTPS => 1f / currentPeriod_;
    
    public event Action? OnTick;
}
