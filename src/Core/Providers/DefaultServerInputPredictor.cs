using Core.Utility;

namespace Core.Providers;

/// <summary>
/// Default implementation of <see cref="IServerInputPredictor{TServerInput,TGameState}"/>.
/// Predicts that the remains unchanged (i.e. does nothing).
/// </summary>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public class DefaultServerInputPredictor<TServerInput, TGameState> : IServerInputPredictor<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : class, new()
{
    /// <inheritdoc/>
    public void PredictInput(ref TServerInput input, TGameState state) { }
}
