using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Timing;

/// <summary>
/// A clock which periodically raises an event.
/// </summary>
interface IClock
{
    /// <summary>
    /// Is evenly called on a clock tick.
    /// </summary>
    event Action OnTick;

    /// <summary>
    /// The target TPS of the clock, the clock should as closely match this TPS while spacing the ticks evenly.
    /// </summary>
    double TargetTps { get; set; }

    /// <summary>
    /// Starts the clock.
    /// </summary>
    /// <param name="cancelToken">Token to stop the clock from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the clock lifetime.</returns>
    Task RunAsync(CancellationToken cancelToken = new());
}
