using System.Diagnostics;
using Serilog;

namespace Core.Utility;

readonly struct UpdateTimer
{
    readonly Stopwatch stopwatch_ = new();
    public required ILogger Logger { private get; init; }

    public UpdateTimer() { }

    public void Start()
    {
        stopwatch_.Start();
    }

    public void End(long frame)
    {
        stopwatch_.Stop();
        Logger.Verbose("Update {Frame} took {Milliseconds}.", frame, stopwatch_.ElapsedMilliseconds);
        stopwatch_.Reset();
    }
}
