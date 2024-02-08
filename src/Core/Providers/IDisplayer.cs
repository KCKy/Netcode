namespace Core.Providers;

/// <summary>
/// Displays the game state to the user (render, audio, etc.).
/// </summary>
/// <remarks>
/// Mostly useful to display the game to the players. It is up to the game programmer to pick the best
/// representation of the two distinct states: the authoritative is final, but delayed, whereas the predict state is immediate
/// but slightly incorrect. Generally it is good to show what results from client inputs as predicted whereas global actions may be
/// better authored.
/// </remarks>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public interface IDisplayer<in TGameState>
{
    /// <summary>
    /// Called when the client is initialized (if on the is client-side).
    /// </summary>
    /// <param name="id">The id of the client. May be used to determine, which data corresponds to the local client.</param>
    public void Init(long id);
    
    /// <summary>
    /// Add current authoritative frame.
    /// </summary>
    /// <param name="frame">The frame index (called in ascending continuous order).</param>
    /// <param name="gameState">Read only borrow of the game state (May not be modified by the displayer).</param>
    public void AddAuthoritative(long frame, TGameState gameState);

    /// <summary>
    /// Add current predict frame (for the client-side).
    /// </summary>
    /// <param name="frame">The frame index (called in ascending continuous order).</param>
    /// <param name="gameState">Read only borrow of the game state (May not be modified by the displayer).</param>
    public void AddPredict(long frame, TGameState gameState);
}
