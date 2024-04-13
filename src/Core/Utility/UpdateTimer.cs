using System.Diagnostics;
using Serilog;

namespace Kcky.GameNewt.Utility;

/// <summary>
/// Timer to measure and log state update times.
/// </summary>
readonly struct UpdateTimer
{
    readonly Stopwatch stopwatch_ = new();

    readonly ILogger logger_ = Log.ForContext<UpdateTimer>();

    public UpdateTimer() { }

    public void Start()
    {
        stopwatch_.Start();
    }

    public void End(long frame)
    {
        stopwatch_.Stop();
        logger_.Verbose("Update {Frame} took {Milliseconds}.", frame, stopwatch_.ElapsedMilliseconds);
        stopwatch_.Reset();
    }
}
