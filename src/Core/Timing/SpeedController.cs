using Serilog;
using Useful;

namespace Core.Timing;

/// <summary>
/// The default <see cref="SpeedController"/> implementation.
/// Sets the speed to approach the desired delta in <see cref="SmoothingTime"/> seconds.
/// </summary>
/// <remarks>
/// Because the current delta is constantly being updated the speed is also constantly updated.
/// Therefore, from purely theoretical point of view the desired delta would never be achieved but only approached.
/// However, for practical use this technique does not overshoot and converges close enough to the desired delta in short time.
/// </remarks>
public class SpeedController : ISpeedController
{
    readonly Clock clock_ = new();

    /// <inheritdoc/>
    public Task RunAsync(CancellationToken cancelToken = new()) => clock_.RunAsync(cancelToken);

    /// <summary>
    /// Constructor.
    /// </summary>
    public SpeedController()
    {
        FrameMemory = 20;
    }

    /// <inheritdoc/>
    public event Action? OnTick
    {
        add => clock_.OnTick += value;
        remove => clock_.OnTick -= value;
    }

    double currentPeriod_ = 0;
    double targetDelta_ = 0;
    double currentDelta_ = 0;
    double targetSpeed_ = 1;

    /// <summary>
    /// The time to smooth over. The controller shall set speed such that the desired delta would be achieved over this time (if no speed change occured after).
    /// </summary>
    public double SmoothingTime { get; init; } = 1;

    readonly ILogger logger_ = Log.ForContext<SpeedController>();
    readonly object mutex_ = new();

    void Update()
    {
        // We need to catch up / lose D seconds over the time of one second.
        double difference =  targetDelta_ - currentDelta_;
        double deltaSpeed = targetSpeed_ * difference / SmoothingTime;

        double newSpeed = Math.Max(0, deltaSpeed + targetSpeed_);

        // The update period in accordance to new speed.
        currentPeriod_ = Math.Min(1 / newSpeed, 1);

        clock_.TargetTps = CurrentTps;

        logger_.Verbose("Setting new period to {currentPeriod_} s. (D : {Difference}, V: {DeltaV})", newSpeed, difference, deltaSpeed);
    }

    /// <inheritdoc/>
    public double TargetTps
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

    /// <inheritdoc/>
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

    /// <summary>
    /// For better results, the controller takes a minimum delay value over a past window (to account for fluctuating connections).
    /// This value determines this window's size in game ticks.
    /// </summary>
    public int FrameMemory
    {
        get => statsWindowed_.Length;
        init
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Memory must have positive size.");
            statsWindowed_ = new(value);
        }
    }

    MinimumWindowed<double> statsWindowed_;

    /// <inheritdoc/>
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
                currentDelta_ = statsWindowed_.Add(value);
                logger_.Verbose("Updated delta to {Delta}", currentDelta_);
                Update();
            }
        }
    }

    /// <inheritdoc/>
    public double CurrentTps
    {
        get
        {
            lock (mutex_)
                return 1f / currentPeriod_;
        }
    }
}
