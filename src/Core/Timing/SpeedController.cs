﻿using System;
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
/// From purely theoretical point of view the desired delta is only being approached.
/// However, this technique does not overshoot and converges close enough to the desired delta in a short time.
/// </remarks>
public class SpeedController : ISpeedController
{
    readonly Clock clock_ = new();

    /// <inheritdoc/>
    public Task RunAsync(CancellationToken cancelToken = new()) => clock_.RunAsync(cancelToken);

    /// <inheritdoc/>
    public event Action? OnTick
    {
        add => clock_.OnTick += value;
        remove => clock_.OnTick -= value;
    }

    double targetDelta_ = 0.05;
    double currentDelta_ = 0;
    double targetSpeed_ = 1;
    double currentSpeed_ = 1;

    /// <inheritdoc/>
    public double TargetNeighborhood
    {
        get
        {
            lock (mutex_)
                return targetNeighborhood_;
        }
        set
        {
            if (!double.IsPositive(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The radius must be positive.");

            lock (mutex_)
            {
                targetNeighborhood_ = value;
                clock_.MaxWaitTime = TimeSpan.FromSeconds(value);
            }
        }
    }

    /// <inheritdoc/>
    public double SmoothingTime
    {
        get
        {
            lock (mutex_)
                return smoothingTime_;
        }
        set
        {
            if (!double.IsPositive(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Smoothing time must be positive.");
            
            lock (mutex_)
            {
                smoothingTime_ = value;
                speedFunctionExponent_ = CalculateSpeedFunctionExponent(smoothingTime_);
            }
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

    static double CalculateSpeedFunctionExponent(double t) => 1 / t + 1;

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
                currentDelta_ = value;
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
