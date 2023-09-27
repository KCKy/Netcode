namespace Core.Providers;

public interface IClock
{
    /// <summary>
    /// Is evenly called on a clock tick.
    /// </summary>
    event Action OnTick;

    /// <summary>
    /// The ticks per second of the server. Constant.
    /// </summary>
    double TargetTPS { get; set; }

    /// <summary>
    /// Starts the clock.
    /// </summary>
    /// <param name="cancelToken">Token to stop the clock from running.</param>
    /// <returns>Infinite task which may be cancelled which represents the clock lifetime.</returns>
    Task RunAsync(CancellationToken cancelToken = new());
}
