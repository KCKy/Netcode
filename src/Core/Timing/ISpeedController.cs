namespace Core.Timing;

/// <summary>
/// Controls update frequency of the games predict loop to stay ahead of the server
/// (to have time to send inputs) while keeping minimum latency.
/// This structure shall be thread safe.
/// </summary>
public interface ISpeedController : IClock
{
    /// <summary>
    /// Number of seconds how much the client should be ahead.
    /// </summary>
    double TargetDelta { get; set; }
    
    /// <summary>
    /// Returns the predict loop TPS, could be slightly off from the server loop to catch up/slow down.
    /// </summary>
    double CurrentTps { get; }

    /// <summary>
    /// Current delay from the server, the controller will modify current tps to so <see cref="CurrentTps"/> would approach this value.
    /// </summary>
    double CurrentDelta { get; set; }
}
