using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Core.Utility;

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

    float currentPeriod_;

    readonly float targetTps_;

    volatile float targetDelta_;
    volatile float currentDelta_;

    public float TargetTPS
    {
        get => targetTps_;
        init
        {
            if (!float.IsPositive(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "TPS must be a positive number.");

            currentPeriod_ = 1 / targetTps_;
            targetTps_ = value;
        }
    }

    const string DeltaMustBeReal = "Delta must be a real number.";

    public float TargetDelta
    {
        get => targetDelta_;
        set
        {
            if (!float.IsRealNumber(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, DeltaMustBeReal);

            targetDelta_ = value;
        }
    }

    public float CurrentDelta
    {
        get => currentDelta_;
        set
        {
            if (!float.IsRealNumber(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, DeltaMustBeReal);

            currentDelta_ = value;
        }
    }

    public float CurrentTPS => 1f / currentPeriod_;
    
    public event Action? OnTick;
}
