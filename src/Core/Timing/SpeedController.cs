using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

    double targetDelta_ = 0;
    double currentDelta_ = 0;
    double targetSpeed_ = 1;
    double currentSpeed_ = 1;

    /// <inheritdoc/>
    public double TargetNeighborhood
    {
        get => targetNeighborhood_;
        set
        {
            if (!double.IsPositive(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The radius must be positive.");
            targetNeighborhood_ = value;
            speedFunctionExponent_ = CalculateSpeedFunctionExponent(smoothingTime_);
        }
    }

    /// <inheritdoc/>
    public double SmoothingTime
    {
        get => smoothingTime_;
        set
        {
            if (!double.IsPositive(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Smoothing time must be positive.");
            smoothingTime_ = value;
            speedFunctionExponent_ = CalculateSpeedFunctionExponent(smoothingTime_);
        }
    }

    readonly ILogger logger_ = Log.ForContext<SpeedController>();
    readonly object mutex_ = new();

    void Update()
    {
        // We need to catch up / lose difference "distance-seconds"
        double difference =  targetDelta_ - currentDelta_;
        double deltaSpeedBase = Math.Abs(difference) / targetNeighborhood_;

        double deltaSpeed = Math.Sign(difference) * Math.Pow(deltaSpeedBase, speedFunctionExponent_);
        double newSpeed = Math.Max(0, deltaSpeed + targetSpeed_); // Speed cannot be negative

        // The update period in accordance to new speed.
        currentSpeed_ = newSpeed;

        clock_.TargetTps = currentSpeed_;
        logger_.Verbose("Setting new speed to {CurrentSpeed} TPS. (D : {Difference}, V: {DeltaV}, E: {Exponent}, O:{Epsilon})", currentSpeed_, difference, deltaSpeed, speedFunctionExponent_, targetNeighborhood_);
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

    static double CalculateSpeedFunctionExponent(double t) => 1 / t + 1;

    MinimumWindowed<double> statsWindowed_;

    const double DefaultTargetNeighborhood = 0.025;
    double targetNeighborhood_ = DefaultTargetNeighborhood;

    const double DefaultSmoothingTime = 1;
    double smoothingTime_ = DefaultSmoothingTime;

    double speedFunctionExponent_ = CalculateSpeedFunctionExponent(DefaultSmoothingTime);

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
                return currentSpeed_;
        }
    }
}
