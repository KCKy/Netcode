using System;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Provides common API of the client usable across games.
/// </summary>
public interface IClient
{
    /// <summary>
    /// The id of the client.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// 
    /// </summary>
    bool TraceState { get; set; }

    /// <summary>
    /// Whether to check if checksums provided by the server match with the states on the client side (server needs to have checksum enabled as well).
    /// </summary>
    bool UseChecksum { get; set; }
    
    /// <summary>
    /// The latest authoritative frame the client has computed.
    /// </summary>
    long AuthFrame { get; }

    /// <summary>
    /// The latest predict frame the client has computed i.e. the frame the clients perception of the game is at.
    /// </summary>
    long PredictFrame { get; }
    
    /// <summary>
    /// Number of seconds, how much the client should be ahead from the server (ignoring latency).
    /// </summary>
    float TargetDelta { get; }
    
    /// <summary>
    /// Returns the predict loop TPS, could be slightly off from the server loop to catch up/slow down.
    /// </summary>
    float CurrentTps { get; }

    /// <summary>
    /// The target TPS of the client clock, the clock should as closely match this TPS while spacing the ticks evenly.
    /// </summary>
    float TargetTps { get; }

    /// <summary>
    /// Begin the client. Starts the update controller for predict, input collection, and server communication.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the client has already been started or terminated.</exception>
    /// <returns>Task representing the client's runtime. When the client crashes the task will be faulted. If the client is stopped it will be cancelled.</returns>
    Task RunAsync();
    
    /// <summary>
    /// Stops the client. Predict updates will stop.
    /// </summary>
    void Terminate();
}
