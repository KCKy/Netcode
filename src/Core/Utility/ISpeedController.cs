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
public interface ISpeedController
{
    /// <summary>
    /// The ticks per second of the server. Constant.
    /// </summary>
    public float TargetTPS { get; init; }

    /// <summary>
    /// Number of seconds which tell how much the loop should be ahead.
    /// </summary>
    public float TargetDelta { get; set; }
    
    /// <summary>
    /// Returns the predict loop TPS, could be sligtly off from the server loop to catch up.
    /// </summary>
    public float CurrentTPS { get; }

    /// <summary>
    /// Current delay from the server, the controller will modify current tps to approach this value.
    /// </summary>
    public float CurrentDelta { get; set; }

    /// <summary>
    /// Is evenly called <see cref="CurrentTPS"/> times per second.
    /// </summary>
    public event Action OnTick;

    /// <summary>
    /// Starts the controller.
    /// </summary>
    /// <param name="cancelToken">Token to stop the controller from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the controller lifetime.</returns>
    public Task RunAsync(CancellationToken cancelToken = new());
}
