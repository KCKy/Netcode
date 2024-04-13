namespace Kcky.GameNewt.Providers;

/// <summary>
/// Server-side component responsible for providing the server's input into the state update.
/// </summary>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public interface IServerInputProvider<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : class, new()
{
    /// <summary>
    /// Get current server input derived from the last state.
    /// </summary>
    /// <param name="info">Read only borrow of the latest authoritative game state (May not be modified by the input provider).</param>
    /// <returns>Undeterministic input based partially on the state.</returns>
    /// <remarks>
    /// This is the place to handle undeterministic events affecting the game state (e.g. loading client names from a database,
    /// undeterministic physics simulation, loading server-side config files, adding randomness).
    /// </remarks>
    TServerInput GetInput(TGameState info);
}
