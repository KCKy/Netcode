namespace Core.Providers;

/// <summary>
/// Provides prediction, what the next client input is going to be for a specific client based on the prior input.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
public interface IClientInputPredictor<TClientInput>
    where TClientInput : class, new()
{
    /// <summary>
    /// Predict input of a specific client for the next frame.
    /// </summary>
    /// <param name="previous">The previous input which is expected to be modified into the prediction.</param>
    void PredictInput(ref TClientInput previous);
}
