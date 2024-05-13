using System;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Server;

/// <summary>
/// Provides common API of the server usable across games.
/// </summary>
public interface IServer
{
    /// <summary>
    /// Whether to log all states in the log.
    /// </summary>
    bool TraceState { get; }

    /// <summary>
    /// Whether to do checksums of game states. The server will calculate the checksum and send it to each client.
    /// </summary>
    bool SendChecksum { get; }

    /// <summary>
    /// Begin the server. Starts the update clock and handling clients.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the server has already been started or terminated.</exception>
    /// <returns>Task representing the servers runtime. When the server crashes the task will be faulted. If the server is stopped it will be cancelled.</returns>
    Task RunAsync();

    /// <summary>
    /// Stops the server. State updates will stop.
    /// </summary>
    void Terminate();
}
