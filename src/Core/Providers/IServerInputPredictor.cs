namespace Kcky.GameNewt.Providers;

/// <summary>
/// Provides prediction, what the next server input is going to be.
/// </summary>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
public interface IServerInputPredictor<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : new()
{
    /// <summary>
    /// Predict the server input based on previous input and state.
    /// </summary>
    /// <param name="input">The previous input, which is going to be modified into the prediction.</param>
    /// <param name="info">Read only borrow of the game state (May not be modified by the predictor).</param>
    /// <remarks>
    /// This is meant for the client side prediction of server's behaviour (e.g. predict undeterministic physics events).
    /// </remarks>
    void PredictInput(ref TServerInput input, TGameState info);
}
