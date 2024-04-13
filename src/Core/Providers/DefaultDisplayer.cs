namespace Kcky.GameNewt.Providers;

/// <summary>
/// Default implementation of <see cref="IDisplayer{TGameState}"/>.
/// Does nothing.
/// </summary>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public class DefaultDisplayer<TGameState> : IDisplayer<TGameState>
{
    /// <inheritdoc/>
    public void AddAuthoritative(long frame, TGameState gameState) { }
    
    /// <inheritdoc/>
    public void AddPredict(long frame, TGameState gameState) { }
    
    /// <inheritdoc/>
    public void Init(long id) { }
}
