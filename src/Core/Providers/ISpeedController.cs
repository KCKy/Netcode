using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utility;

/// <summary>
/// Controls update frequency of the games predict loop to stay ahead of the server
/// (to have time to send inputs) while keeping minimum latency.
/// This structure shall be thread safe.
/// </summary>
public interface ISpeedController : IClock
{
    /// <summary>
    /// The ticks per second of the server. Constant.
    /// </summary>
    float TargetTPS { get; init; }

    /// <summary>
    /// Number of seconds which tell how much the loop should be ahead.
    /// </summary>
    float TargetDelta { get; set; }
    
    /// <summary>
    /// Returns the predict loop TPS, could be sligtly off from the server loop to catch up.
    /// </summary>
    float CurrentTPS { get; }

    /// <summary>
    /// Current delay from the server, the controller will modify current tps to approach this value.
    /// </summary>
    float CurrentDelta { get; set; }
}

public interface IClock
{
    /// <summary>
    /// Is evenly called on a clock tick.
    /// </summary>
    event Action OnTick;

    /// <summary>
    /// Starts the clock.
    /// </summary>
    /// <param name="cancelToken">Token to stop the clock from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the clock lifetime.</returns>
    Task RunAsync(CancellationToken cancelToken = new());
}
