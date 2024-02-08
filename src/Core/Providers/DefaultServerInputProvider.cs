namespace Core.Providers;

/// <summary>
/// The default implementation of the <see cref="IServerInputProvider{TServerInput,TGameState}"/>.
/// Uses the parameterless constructor to create the default input instance.
/// </summary>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public class DefaultServerInputProvider<TServerInput, TGameState> : IServerInputProvider<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : class, new()
{
    /// <inheritdoc/>
    public TServerInput GetInput(TGameState info) => new();
}
