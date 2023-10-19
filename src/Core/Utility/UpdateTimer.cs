using System.Diagnostics;
using Serilog;

namespace Core.Utility;

readonly struct UpdateTimer
{
    readonly Stopwatch stopwatch_ = new();
    readonly ILogger logger_;

    public UpdateTimer(ILogger logger)
    {
        logger_ = logger;
    }

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
